using Homer.NetDaemon.Entities;
using Homer.NetDaemon.Services;
using NetDaemon.AppModel;
using NetDaemon.Extensions.Scheduler;
using NetDaemon.HassModel.Entities;
using System.Reactive.Concurrency;
using System.Reactive.Linq;

namespace Homer.NetDaemon.Apps.Bathroom;

[NetDaemonApp]
public class WaterHeater
{
    private const int DailyBudgetMinutes = 120;
    private static readonly TimeSpan ShowerDetectionConfirmationDelay = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan RecoveryShowerDurationThreshold = TimeSpan.FromMinutes(5);
    // Five minutes is the anti-short-cycle floor; budget and max runtime still take priority.
    private static readonly TimeSpan MinimumHeaterRunDuration = TimeSpan.FromMinutes(7);
    private static readonly TimeSpan MaxHeaterRunDuration = TimeSpan.FromMinutes(20);
    private const double PostShowerRecoveryMultiplier = 1.5;

    private readonly ILogger<WaterHeater> _logger;
    private readonly InputBooleanEntity _bathroomPresence;
    private readonly InputBooleanEntity _masterBathroomPresence;
    private readonly InputNumberEntity _waterHeaterMinutesLeft;
    private readonly SwitchEntities _switchEntities;
    private readonly IScheduler _scheduler;
    private readonly BathroomStatusService _bathroomStatusService;
    private readonly WaterHeaterTimerService _waterHeaterTimerService;
    private readonly object _gate = new();
    private IDisposable? _scheduledTurnOff;
    private DateTime? _scheduledTurnOffDateTimeUtc;
    private DateTime? _currentRunStartedAtUtc;
    private DateTime? _postShowerRecoveryUntilUtc;
    private double? _minutesLeftOverride;
    private bool? _waterHeaterIsOnOverride;

    private readonly BathroomShowerState _bathroomState = new();
    private readonly BathroomShowerState _masterBathroomState = new();

    private class BathroomShowerState : IDisposable
    {
        public IDisposable? ShowerConfirmation { get; set; }
        public bool IsShoweringDetected { get; set; }
        public DateTime? ShowerStartTimeUtc { get; set; }

        public void Dispose()
        {
            ShowerConfirmation?.Dispose();
            ShowerConfirmation = null;
        }
    }

    public WaterHeater(
        ILogger<WaterHeater> logger,
        SwitchEntities switchEntities, 
        BinarySensorEntities motionSensors,
        InputBooleanEntities inputBooleanEntities,
        InputNumberEntities inputNumberEntities,
        BathroomStatusService bathroomStatusService,
        WaterHeaterTimerService waterHeaterTimerService,
        IScheduler scheduler)
    {
        _logger = logger;
        _switchEntities = switchEntities;
        _scheduler = scheduler;
        _bathroomStatusService = bathroomStatusService;
        _waterHeaterTimerService = waterHeaterTimerService;
        _bathroomPresence = inputBooleanEntities.BathroomPresence;
        _masterBathroomPresence = inputBooleanEntities.MasterBathroomPresence;
        _waterHeaterMinutesLeft = inputNumberEntities.WaterHeaterMinutesLeft;

        // Mirror Home Assistant's helper locally so budget checks are immediate after service calls.
        _waterHeaterMinutesLeft.StateAllChanges()
            .Subscribe(e =>
            {
                if (e.New?.State is { } minutesLeft)
                {
                    lock (_gate)
                    {
                        _minutesLeftOverride = Math.Clamp(minutesLeft, 0, DailyBudgetMinutes);
                    }
                }
            });

        _switchEntities.WaterHeaterSwitch.StateChanges()
            .Subscribe(e =>
            {
                lock (_gate)
                {
                    // Any external/manual turn-on must still have a budgeted off constraint.
                    if (e.New.IsOn())
                    {
                        _waterHeaterIsOnOverride = true;

                        if (!HasActiveTurnOffConstraint(DateTime.UtcNow))
                        {
                            _logger.LogWarning(
                                "Water heater was turned on without a budgeted turn-off constraint. Turning it off.");
                            TurnHeaterOffCore(
                                "it was turned on without checking the water heater budget",
                                refundUnusedAllocation: false);
                        }

                        return;
                    }

                    if (e.New.IsOff())
                    {
                        _waterHeaterIsOnOverride = false;
                        ReleaseUnusedAllocation();
                        ClearTurnOffConstraint();
                        ReevaluateHeaterCore("the water heater turned off");
                    }
                }
            });

        // The daily allowance starts over at midnight.
        _scheduler.ScheduleCron("0 0 * * *", ResetDailyBudget);

        var bathroomMotionSensors = new[]
        {
            motionSensors.BathroomDoorMotionOccupancy,
            motionSensors.BathroomSinkMotionOccupancy
        };
        SetupBathroomMonitoring("Bathroom", _bathroomPresence, bathroomMotionSensors, _bathroomState);

        var masterBathroomMotionSensors = new[]
        {
            motionSensors.MasterBathroomSinkMotionOccupancy,
            motionSensors.MasterBathroomToiletMotionOccupancy
        };
        SetupBathroomMonitoring("Master Bathroom", _masterBathroomPresence, masterBathroomMotionSensors, _masterBathroomState);

        if (IsHeaterOn)
        {
            TurnHeaterOffCore(
                "it was already on when the app started without a budgeted turn-off constraint",
                refundUnusedAllocation: false);
        }

        lock (_gate)
        {
            ReevaluateHeaterCore("the app started");
        }
    }

    private void SetupBathroomMonitoring(
        string bathroomName,
        InputBooleanEntity presence,
        BinarySensorEntity[] motionSensors,
        BathroomShowerState state)
    {
        Observable.Merge(motionSensors.Select(m => m.StateChanges()))
            .Subscribe(e =>
            {
                lock (_gate)
                {
                    if (state.IsShoweringDetected && e.New.IsOn())
                    {
                        OnShowerEnded(
                            bathroomName,
                            presence,
                            state,
                            $"{bathroomName} motion was detected after the shower started");
                        return;
                    }

                    EvaluateShowerState(bathroomName, presence, motionSensors, state);
                }
            });

        presence.StateChanges().Subscribe(_ =>
        {
            lock (_gate)
            {
                if (presence.IsOff())
                {
                    CancelShowerConfirmation(state);

                    if (state.IsShoweringDetected)
                    {
                        OnShowerEnded(
                            bathroomName,
                            presence,
                            state,
                            $"{bathroomName} became unoccupied");
                    }
                    else
                    {
                        UpdateBathroomStatus(bathroomName, presence, state);
                        ReevaluateHeaterCore($"{bathroomName} became unoccupied");
                    }

                    return;
                }

                EvaluateShowerState(bathroomName, presence, motionSensors, state);
                UpdateBathroomStatus(bathroomName, presence, state);
                ReevaluateHeaterCore($"{bathroomName} became occupied");
            }
        });

        EvaluateShowerState(bathroomName, presence, motionSensors, state);
    }

    private void EvaluateShowerState(
        string bathroomName,
        InputBooleanEntity presence,
        BinarySensorEntity[] motionSensors,
        BathroomShowerState state)
    {
        if (state.IsShoweringDetected)
        {
            return;
        }

        var showerCandidate = presence.IsOn() && motionSensors.All(m => m.IsOff());
        // Presence without motion can mean the person is standing still in the shower.
        if (!showerCandidate)
        {
            CancelShowerConfirmation(state);
            return;
        }

        if (state.ShowerConfirmation is not null)
        {
            return;
        }

        _logger.LogInformation(
            "{BathroomName} has presence with no shower motion. Confirming shower state in {Delay:g}",
            bathroomName,
            ShowerDetectionConfirmationDelay);

        state.ShowerConfirmation = _scheduler.Schedule(ShowerDetectionConfirmationDelay, () =>
        {
            lock (_gate)
            {
                state.ShowerConfirmation = null;

                if (state.IsShoweringDetected ||
                    presence.IsOff() ||
                    motionSensors.Any(m => m.IsOn()))
                {
                    UpdateBathroomStatus(bathroomName, presence, state);
                    ReevaluateHeaterCore($"{bathroomName} shower confirmation was cancelled");
                    return;
                }

                OnShowerStarted(bathroomName, presence, state);
            }
        });
    }

    private void OnShowerStarted(string bathroomName, InputBooleanEntity presence, BathroomShowerState state)
    {
        _logger.LogInformation(
            "{BathroomName} shower confirmed after {Delay:g}; turning on the water heater",
            bathroomName,
            ShowerDetectionConfirmationDelay);

        state.IsShoweringDetected = true;
        state.ShowerStartTimeUtc = DateTime.UtcNow;
        UpdateBathroomStatus(bathroomName, presence, state);
        ReevaluateHeaterCore($"{bathroomName} showering was detected");
    }

    private void OnShowerEnded(
        string bathroomName,
        InputBooleanEntity presence,
        BathroomShowerState state,
        string reason)
    {
        var now = DateTime.UtcNow;
        var showerDuration = state.ShowerStartTimeUtc.HasValue
            ? now - state.ShowerStartTimeUtc.Value
            : TimeSpan.Zero;
        var recoveryDuration = showerDuration > RecoveryShowerDurationThreshold
            ? TimeSpan.FromMinutes(Math.Max(0, showerDuration.TotalMinutes * PostShowerRecoveryMultiplier))
            : TimeSpan.Zero;
        var recoveryUntil = now.Add(recoveryDuration);

        _logger.LogInformation(
            "{BathroomName} shower ended after {ShowerMinutes:F1} minutes. Keeping the water heater available for {RecoveryMinutes:F1} recovery minutes. Reason: {Reason}",
            bathroomName,
            showerDuration.TotalMinutes,
            recoveryDuration.TotalMinutes,
            reason);

        CancelShowerConfirmation(state);
        state.IsShoweringDetected = false;
        state.ShowerStartTimeUtc = null;

        if (recoveryDuration > TimeSpan.Zero &&
            (_postShowerRecoveryUntilUtc is null || recoveryUntil > _postShowerRecoveryUntilUtc))
        {
            _postShowerRecoveryUntilUtc = recoveryUntil;
        }

        UpdateBathroomStatus(bathroomName, presence, state);
        ReevaluateHeaterCore($"{bathroomName} shower ended");
    }

    private void ReevaluateHeaterCore(string reason)
    {
        // Active showers and post-shower recovery are the only reasons the heater may stay on.
        if (AnyShowerActive)
        {
            EnsureHeaterOnCore($"{reason} and a shower is active", MaxHeaterRunDuration);
            return;
        }

        var now = DateTime.UtcNow;
        if (_postShowerRecoveryUntilUtc <= now)
        {
            _postShowerRecoveryUntilUtc = null;
        }

        if (_postShowerRecoveryUntilUtc is { } recoveryUntil)
        {
            var remainingRecovery = recoveryUntil - now;
            if (!IsHeaterOn && remainingRecovery < MinimumHeaterRunDuration)
            {
                _logger.LogInformation(
                    "Skipping post-shower recovery because only {Minutes:F1} minutes remain, below the minimum heater run duration of {MinimumMinutes:F1} minutes",
                    remainingRecovery.TotalMinutes,
                    MinimumHeaterRunDuration.TotalMinutes);
                _postShowerRecoveryUntilUtc = null;
            }
            else
            {
                EnsureHeaterOnCore(
                    $"{reason} and post-shower recovery is active",
                    remainingRecovery);
                return;
            }
        }

        if (IsHeaterOn)
        {
            TurnHeaterOffAfterMinimumRunCore($"{reason} and no shower or recovery is active");
        }
    }

    private void TurnHeaterOffAfterMinimumRunCore(string reason)
    {
        var now = DateTime.UtcNow;
        if (_currentRunStartedAtUtc is { } runStartedAt && HasActiveTurnOffConstraint(now))
        {
            var minimumTurnOff = runStartedAt.Add(MinimumHeaterRunDuration);
            if (minimumTurnOff > now)
            {
                EnsureHeaterOnCore(
                    $"{reason}; waiting for the minimum heater run duration",
                    minimumTurnOff - now);
                return;
            }
        }

        TurnHeaterOffCore(reason, refundUnusedAllocation: true);
    }

    private void EnsureHeaterOnCore(string reason, TimeSpan requestedDuration)
    {
        if (requestedDuration <= TimeSpan.Zero)
        {
            return;
        }

        var now = DateTime.UtcNow;
        if (!IsHeaterOn)
        {
            StartConstrainedRunCore(reason, requestedDuration, now);
            return;
        }

        if (!HasActiveTurnOffConstraint(now))
        {
            TurnHeaterOffCore(
                $"{reason}, but the water heater was already on without a valid budgeted turn-off constraint",
                refundUnusedAllocation: false);
            StartConstrainedRunCore(reason, requestedDuration, DateTime.UtcNow);
            return;
        }

        var currentDeadline = _scheduledTurnOffDateTimeUtc!.Value;
        var maxDeadline = (_currentRunStartedAtUtc ?? now).Add(MaxHeaterRunDuration);
        var requestedDeadline = now.Add(requestedDuration);
        var desiredDeadline = ApplyMinimumRunDurationFloor(now, Min(requestedDeadline, maxDeadline));

        if (desiredDeadline <= now)
        {
            TurnHeaterOffCore($"{reason}, but the constrained run has no time left", refundUnusedAllocation: false);
            return;
        }

        if (desiredDeadline < currentDeadline)
        {
            var unusedAllocation = currentDeadline - desiredDeadline;
            // Return time to the budget when a newer constraint shortens this run.
            RefundBudget(unusedAllocation);
            ScheduleTurnOffCore(
                desiredDeadline,
                $"{reason}; shortened to match the requested heater duration");
            return;
        }

        if (desiredDeadline == currentDeadline)
        {
            return;
        }

        var extension = AllocateBudget(desiredDeadline - currentDeadline);
        if (extension <= TimeSpan.Zero)
        {
            _logger.LogInformation(
                "Water heater is already on, but no budget remains to extend it for {Reason}. Current constrained turn-off remains {TurnOffTime:u}",
                reason,
                currentDeadline);
            return;
        }

        ScheduleTurnOffCore(
            currentDeadline.Add(extension),
            $"{reason}; extended within the remaining water heater budget");
    }

    private DateTime ApplyMinimumRunDurationFloor(DateTime nowUtc, DateTime desiredDeadlineUtc)
    {
        if (_currentRunStartedAtUtc is not { } runStartedAt)
        {
            return desiredDeadlineUtc;
        }

        var minimumDeadline = runStartedAt.Add(MinimumHeaterRunDuration);
        return minimumDeadline > nowUtc && desiredDeadlineUtc < minimumDeadline
            ? minimumDeadline
            : desiredDeadlineUtc;
    }

    private void StartConstrainedRunCore(string reason, TimeSpan requestedDuration, DateTime now)
    {
        var constrainedDuration = Min(requestedDuration, MaxHeaterRunDuration);
        // Budget is deducted before the switch is turned on.
        var allocatedDuration = AllocateBudget(constrainedDuration);

        if (allocatedDuration <= TimeSpan.Zero)
        {
            _logger.LogInformation(
                "Skipping water heater turn-on for {Reason} because Water Heater Minutes Left is {MinutesLeft:F2}",
                reason,
                GetBudgetMinutesLeft());
            return;
        }

        _currentRunStartedAtUtc = now;
        _waterHeaterTimerService.LastTurnedOnDateTime = now;
        ScheduleTurnOffCore(now.Add(allocatedDuration), reason);

        _logger.LogInformation(
            "Turning on water heater for {AllocatedMinutes:F2} minutes because {Reason}. Water Heater Minutes Left is now {MinutesLeft:F2}",
            allocatedDuration.TotalMinutes,
            reason,
            GetBudgetMinutesLeft());

        _waterHeaterIsOnOverride = true;
        _switchEntities.WaterHeaterSwitch.TurnOn();
    }

    private TimeSpan AllocateBudget(TimeSpan requestedDuration)
    {
        if (requestedDuration <= TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        var minutesLeft = GetBudgetMinutesLeft();
        var allocatedMinutes = Math.Min(requestedDuration.TotalMinutes, minutesLeft);

        if (allocatedMinutes <= 0)
        {
            return TimeSpan.Zero;
        }

        SetBudgetMinutesLeft(minutesLeft - allocatedMinutes);
        return TimeSpan.FromMinutes(allocatedMinutes);
    }

    private void RefundBudget(TimeSpan unusedAllocation)
    {
        if (unusedAllocation <= TimeSpan.Zero)
        {
            return;
        }

        SetBudgetMinutesLeft(GetBudgetMinutesLeft() + unusedAllocation.TotalMinutes);
    }

    private void ReleaseUnusedAllocation()
    {
        if (_scheduledTurnOffDateTimeUtc is not { } scheduledTurnOff)
        {
            return;
        }

        var unusedAllocation = scheduledTurnOff - DateTime.UtcNow;
        RefundBudget(unusedAllocation);
    }

    private void TurnHeaterOffCore(string reason, bool refundUnusedAllocation)
    {
        if (refundUnusedAllocation)
        {
            ReleaseUnusedAllocation();
        }

        ClearTurnOffConstraint();

        if (!IsHeaterOn)
        {
            return;
        }

        _logger.LogInformation("Turning off water heater because {Reason}", reason);
        _waterHeaterIsOnOverride = false;
        _switchEntities.WaterHeaterSwitch.TurnOff();
    }

    private void ScheduleTurnOffCore(DateTime turnOffDateTimeUtc, string reason)
    {
        _scheduledTurnOff?.Dispose();
        _scheduledTurnOff = null;

        // This scheduled action is the hard constraint paired with every allowed run.
        _scheduledTurnOffDateTimeUtc = turnOffDateTimeUtc;
        _waterHeaterTimerService.ScheduledTurnOffDateTime = turnOffDateTimeUtc;

        var delay = turnOffDateTimeUtc - DateTime.UtcNow;
        if (delay < TimeSpan.Zero)
        {
            delay = TimeSpan.Zero;
        }

        _logger.LogInformation(
            "Scheduled water heater turn-off at {TurnOffTime:u}. Reason: {Reason}",
            turnOffDateTimeUtc,
            reason);

        _scheduledTurnOff = _scheduler.Schedule(delay, () =>
        {
            lock (_gate)
            {
                if (_scheduledTurnOffDateTimeUtc != turnOffDateTimeUtc)
                {
                    return;
                }

                TurnHeaterOffCore(
                    $"the budgeted turn-off constraint elapsed at {turnOffDateTimeUtc:u}",
                    refundUnusedAllocation: false);
            }
        });
    }

    private void ClearTurnOffConstraint()
    {
        _scheduledTurnOff?.Dispose();
        _scheduledTurnOff = null;
        _scheduledTurnOffDateTimeUtc = null;
        _currentRunStartedAtUtc = null;
        _waterHeaterTimerService.ScheduledTurnOffDateTime = null;
    }

    private bool HasActiveTurnOffConstraint(DateTime nowUtc)
    {
        return _scheduledTurnOff is not null &&
               _scheduledTurnOffDateTimeUtc is { } scheduledTurnOff &&
               scheduledTurnOff > nowUtc;
    }

    private void CancelShowerConfirmation(BathroomShowerState state)
    {
        state.ShowerConfirmation?.Dispose();
        state.ShowerConfirmation = null;
    }

    private void ResetDailyBudget()
    {
        lock (_gate)
        {
            SetBudgetMinutesLeft(DailyBudgetMinutes);
            _logger.LogInformation("Reset Water Heater Minutes Left to {Minutes}", DailyBudgetMinutes);
        }
    }

    private double GetBudgetMinutesLeft()
    {
        var minutesLeft = _minutesLeftOverride ?? _waterHeaterMinutesLeft.State ?? 0;
        return double.IsNaN(minutesLeft) || double.IsInfinity(minutesLeft)
            ? 0
            : Math.Clamp(minutesLeft, 0, DailyBudgetMinutes);
    }

    private void SetBudgetMinutesLeft(double minutesLeft)
    {
        var clamped = Math.Clamp(minutesLeft, 0, DailyBudgetMinutes);
        var rounded = Math.Round(clamped, 2, MidpointRounding.AwayFromZero);

        _minutesLeftOverride = rounded;
        _waterHeaterMinutesLeft.SetValue(rounded);
    }

    private bool IsHeaterOn => _waterHeaterIsOnOverride ?? _switchEntities.WaterHeaterSwitch.IsOn();

    private static TimeSpan Min(TimeSpan left, TimeSpan right) => left <= right ? left : right;

    private static DateTime Min(DateTime left, DateTime right) => left <= right ? left : right;

    private bool AnyShowerActive => _bathroomState.IsShoweringDetected || _masterBathroomState.IsShoweringDetected;

    private void UpdateBathroomStatus(string bathroomName, InputBooleanEntity presence, BathroomShowerState state)
    {
        BathroomState status;

        // Determine the bathroom status based on presence and showering state
        if (state.IsShoweringDetected)
        {
            status = BathroomState.Showering;
        }
        else if (presence.IsOn())
        {
            status = BathroomState.Occupied;
        }
        else
        {
            status = BathroomState.Unoccupied;
        }

        // Update the service
        if (bathroomName == "Bathroom")
        {
            _bathroomStatusService.BathroomStatus = status;
        }
        else if (bathroomName == "Master Bathroom")
        {
            _bathroomStatusService.MasterBathroomStatus = status;
        }
    }
}
