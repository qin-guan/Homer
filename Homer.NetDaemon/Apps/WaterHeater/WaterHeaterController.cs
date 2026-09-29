using Homer.NetDaemon.Entities;
using Homer.NetDaemon.Services;
using NetDaemon.AppModel;
using NetDaemon.Extensions.Scheduler;
using NetDaemon.HassModel.Entities;
using System.Reactive.Concurrency;

namespace Homer.NetDaemon.Apps.WaterHeater;

/// <summary>
/// Owns the water heater switch: every run is deducted from a daily budget and paired with a scheduled turn-off, and
/// any turn-on that bypasses this is reverted. Other apps and the dashboard request heating through
/// <see cref="WaterHeaterTimerService"/>; while this app is disabled those requests are ignored.
/// </summary>
[NetDaemonApp]
public sealed class WaterHeaterController : IWaterHeaterController, IDisposable
{
    private const int DailyBudgetMinutes = 120;
    // Anti-short-cycle floor; budget and max runtime still take priority.
    public static readonly TimeSpan MinimumHeaterRunDuration = TimeSpan.FromMinutes(7);
    public static readonly TimeSpan MaxHeaterRunDuration = TimeSpan.FromMinutes(25);

    private readonly ILogger<WaterHeaterController> _logger;
    private readonly InputNumberEntity _waterHeaterMinutesLeft;
    private readonly SwitchEntities _switchEntities;
    private readonly IScheduler _scheduler;
    private readonly WaterHeaterTimerService _waterHeaterTimerService;
    private readonly object _gate = new();
    private IDisposable? _scheduledTurnOff;
    private DateTime? _scheduledTurnOffDateTimeUtc;
    private DateTime? _currentRunStartedAtUtc;
    private double? _minutesLeftOverride;
    private bool? _waterHeaterIsOnOverride;

    public WaterHeaterController(
        ILogger<WaterHeaterController> logger,
        SwitchEntities switchEntities,
        InputNumberEntities inputNumberEntities,
        WaterHeaterTimerService waterHeaterTimerService,
        IScheduler scheduler)
    {
        _logger = logger;
        _switchEntities = switchEntities;
        _scheduler = scheduler;
        _waterHeaterTimerService = waterHeaterTimerService;
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

                    if (!e.New.IsOff())
                    {
                        return;
                    }

                    _waterHeaterIsOnOverride = false;
                    ReleaseUnusedAllocation();
                    ClearTurnOffConstraint();
                }

                // Outside the lock: listeners such as shower heating request heating again from this controller.
                _waterHeaterTimerService.OnHeaterTurnedOff();
            });

        // The daily allowance starts over at midnight.
        _scheduler.ScheduleCron("0 0 * * *", ResetDailyBudget);

        lock (_gate)
        {
            if (IsHeaterOnCore)
            {
                TurnHeaterOffCore(
                    "it was already on when the app started without a budgeted turn-off constraint",
                    refundUnusedAllocation: false);
            }
        }

        _waterHeaterTimerService.AttachController(this);
    }

    public bool IsHeaterOn
    {
        get
        {
            lock (_gate)
            {
                return IsHeaterOnCore;
            }
        }
    }

    public void RequestHeating(string reason, TimeSpan duration)
    {
        lock (_gate)
        {
            EnsureHeaterOnCore(reason, duration);
        }
    }

    public void ReleaseHeating(string reason)
    {
        lock (_gate)
        {
            if (IsHeaterOnCore)
            {
                TurnHeaterOffAfterMinimumRunCore(reason);
            }
        }
    }

    public void Dispose()
    {
        _waterHeaterTimerService.DetachController(this);

        lock (_gate)
        {
            // The scheduled turn-off is cancelled along with this app. Hand the deadline to the hosted turn-off
            // service so disabling the controller never leaves the heater running unbounded.
            if (IsHeaterOnCore && _scheduledTurnOffDateTimeUtc is { } scheduledTurnOff)
            {
                var remaining = scheduledTurnOff - DateTime.UtcNow;
                Channels.Channels.TurnOffWaterHeaterSwitch.Writer.TryWrite(
                    remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero);
            }

            _scheduledTurnOff?.Dispose();
            _scheduledTurnOff = null;
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
        if (!IsHeaterOnCore)
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

        if (!IsHeaterOnCore)
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

    private bool IsHeaterOnCore => _waterHeaterIsOnOverride ?? _switchEntities.WaterHeaterSwitch.IsOn();

    private static TimeSpan Min(TimeSpan left, TimeSpan right) => left <= right ? left : right;

    private static DateTime Min(DateTime left, DateTime right) => left <= right ? left : right;
}
