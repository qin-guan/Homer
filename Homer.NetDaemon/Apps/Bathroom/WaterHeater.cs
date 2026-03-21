using Homer.NetDaemon.Entities;
using Homer.NetDaemon.Services;
using NetDaemon.AppModel;
using NetDaemon.HassModel.Entities;
using System.Reactive.Concurrency;
using System.Reactive.Linq;

namespace Homer.NetDaemon.Apps.Bathroom;

[NetDaemonApp]
public class WaterHeater
{
    private const int ShowerDetectionThresholdSeconds = 300;
    private const int PeriodicCheckIntervalSeconds = 30;
    private const int ShowerEndGracePeriodSeconds = 10;
    private const int MaxContinuousHeaterOnDurationMinutes = 45;
    private const int BasePostShowerRecoveryMinutes = 15;
    private const double AdditionalRecoveryPerShowerMinute = 0.5;

    private readonly ILogger<WaterHeater> _logger;
    private readonly InputBooleanEntity _bathroomPresence;
    private readonly InputBooleanEntity _masterBathroomPresence;
    private readonly SwitchEntities _switchEntities;
    private readonly IScheduler _scheduler;
    private readonly BathroomStatusService _bathroomStatusService;
    private readonly WaterHeaterTimerService _waterHeaterTimerService;
    private readonly NotifyServices _notifyServices;
    private IDisposable? _scheduledTurnOff;
    private IDisposable? _maxContinuousOnTurnOff;
    private bool _currentHeatingSessionHadShower;
    private TimeSpan _currentHeatingSessionShowerDuration = TimeSpan.Zero;

    // Track state for each bathroom separately
    private readonly BathroomShowerState _bathroomState = new();
    private readonly BathroomShowerState _masterBathroomState = new();

    private class BathroomShowerState : IDisposable
    {
        public IDisposable? PeriodicCheck { get; set; }
        public bool IsShoweringDetected { get; set; }
        public DateTime? LastMotionTime { get; set; }
        public DateTime? FirstMotionAfterShowerStart { get; set; }
        public DateTime? ShowerStartTime { get; set; }

        public void Dispose()
        {
            PeriodicCheck?.Dispose();
            PeriodicCheck = null;
        }
    }

    public WaterHeater(
        ILogger<WaterHeater> logger,
        SwitchEntities switchEntities, 
        BinarySensorEntities motionSensors,
        InputBooleanEntities inputBooleanEntities,
        BathroomStatusService bathroomStatusService,
        WaterHeaterTimerService waterHeaterTimerService,
        NotifyServices notifyServices,
        IScheduler scheduler)
    {
        _logger = logger;
        _switchEntities = switchEntities;
        _scheduler = scheduler;
        _bathroomStatusService = bathroomStatusService;
        _waterHeaterTimerService = waterHeaterTimerService;
        _notifyServices = notifyServices;
        _bathroomPresence = inputBooleanEntities.BathroomPresence;
        _masterBathroomPresence = inputBooleanEntities.MasterBathroomPresence;

        // Setup monitoring for regular bathroom
        var bathroomMotionSensors = new[]
        {
            motionSensors.BathroomSinkMotionOccupancy
        };
        SetupBathroomMonitoring("Bathroom", _bathroomPresence, bathroomMotionSensors, _bathroomState);

        // Setup monitoring for master bathroom
        var masterBathroomMotionSensors = new[]
        {
            motionSensors.MasterBathroomSinkMotionOccupancy,
            motionSensors.MasterBathroomToiletMotionOccupancy
        };
        SetupBathroomMonitoring("Master Bathroom", _masterBathroomPresence, masterBathroomMotionSensors, _masterBathroomState);

        if (_switchEntities.WaterHeaterSwitch.IsOn())
        {
            var turnedOnAt = _waterHeaterTimerService.LastTurnedOnDateTime ?? DateTime.UtcNow;
            _waterHeaterTimerService.LastTurnedOnDateTime = turnedOnAt;
            _waterHeaterTimerService.ScheduledTurnOffDateTime =
                turnedOnAt.AddMinutes(MaxContinuousHeaterOnDurationMinutes);
            ScheduleMaxContinuousOnTurnOff(turnedOnAt);
        }

        ReevaluateHeater("the app started");
    }

    private void SetupBathroomMonitoring(
        string bathroomName,
        InputBooleanEntity presence,
        BinarySensorEntity[] motionSensors,
        BathroomShowerState state)
    {
        // Track motion sensor changes
        Observable.Merge(motionSensors.Select(m => m.StateChanges()))
            .Where(e => e.New.IsOn())
            .Subscribe(_ =>
            {
                state.LastMotionTime = DateTime.UtcNow;

                if (state.IsShoweringDetected)
                {
                    // Track first motion after shower starts for grace period
                    state.FirstMotionAfterShowerStart ??= DateTime.UtcNow;
                }

                CheckShoweringState(bathroomName, presence, motionSensors, state);
            });

        // Monitor bathroom presence changes
        presence.StateChanges().Subscribe(_ =>
        {
            if (presence.IsOn())
            {
                // Presence detected, update status and start periodic monitoring for shower
                state.LastMotionTime = DateTime.UtcNow;
                UpdateBathroomStatus(bathroomName, presence, state);
                state.PeriodicCheck?.Dispose();

                // Schedule periodic checks starting after initial delay
                var checkState = (name: bathroomName, presence, sensors: motionSensors, state);
                state.PeriodicCheck = _scheduler.SchedulePeriodic(
                    checkState,
                    TimeSpan.FromSeconds(PeriodicCheckIntervalSeconds),
                    s => CheckShoweringState(s.name, s.presence, s.sensors, s.state)
                );

                ReevaluateHeater($"{bathroomName} became occupied");
            }
            else
            {
                // Presence ended, stop monitoring
                state.PeriodicCheck?.Dispose();
                state.PeriodicCheck = null;

                if (state.IsShoweringDetected)
                {
                    OnShowerEnded(bathroomName, presence, state);
                }
                
                UpdateBathroomStatus(bathroomName, presence, state);
                ReevaluateHeater($"{bathroomName} became unoccupied");
            }
        });
    }

    private void CheckShoweringState(
        string bathroomName,
        InputBooleanEntity presence,
        BinarySensorEntity[] motionSensors,
        BathroomShowerState state)
    {
        var presenceOn = presence.IsOn();
        var anyMotion = motionSensors.Any(m => m.IsOn());

        // Calculate time since last motion (null means no motion detected yet)
        var timeSinceLastMotion = state.LastMotionTime.HasValue
            ? DateTime.UtcNow - state.LastMotionTime.Value
            : TimeSpan.MaxValue;

        // User is showering if:
        // 1. Presence is detected (person is in bathroom)
        // 2. No motion sensors are currently triggered
        // 3. It's been at least ShowerDetectionThresholdSeconds since last motion (gives time to enter shower)
        var isShowering = presenceOn && !anyMotion && timeSinceLastMotion > TimeSpan.FromSeconds(ShowerDetectionThresholdSeconds);

        if (isShowering && !state.IsShoweringDetected)
        {
            // Start of shower detected
            _logger.LogInformation("{BathroomName} shower detected - turning on water heater", bathroomName);
            state.IsShoweringDetected = true;
            state.ShowerStartTime = DateTime.UtcNow;
            state.FirstMotionAfterShowerStart = null;

            // Update status
            UpdateBathroomStatus(bathroomName, presence, state);

            EnsureHeaterOn($"{bathroomName} showering was detected");
        }
        else if (state.IsShoweringDetected && anyMotion && state.FirstMotionAfterShowerStart.HasValue)
        {
            // Check if motion has been sustained for the grace period
            var timeSinceFirstMotion = DateTime.UtcNow - state.FirstMotionAfterShowerStart.Value;
            if (timeSinceFirstMotion > TimeSpan.FromSeconds(ShowerEndGracePeriodSeconds))
            {
                // Sustained motion detected - end of shower
                OnShowerEnded(bathroomName, presence, state);
            }
        }
    }

    private void OnShowerEnded(string bathroomName, InputBooleanEntity presence, BathroomShowerState state)
    {
        // Calculate shower duration
        var showerDuration = state.ShowerStartTime.HasValue
            ? DateTime.UtcNow - state.ShowerStartTime.Value
            : TimeSpan.Zero;
        
        _logger.LogInformation(
            "{BathroomName} shower ended after {ShowerMinutes:F1} minutes", 
            bathroomName,
            showerDuration.TotalMinutes);

        _currentHeatingSessionHadShower = true;
        _currentHeatingSessionShowerDuration += showerDuration;
        
        state.IsShoweringDetected = false;
        state.FirstMotionAfterShowerStart = null;
        state.ShowerStartTime = null;

        // Update status
        UpdateBathroomStatus(bathroomName, presence, state);

        ReevaluateHeater($"{bathroomName} shower ended after {showerDuration.TotalMinutes:F1} minutes");
    }

    private void ScheduleMaxContinuousOnTurnOff(DateTime turnedOnAt)
    {
        _maxContinuousOnTurnOff?.Dispose();
        _maxContinuousOnTurnOff = _scheduler.Schedule(
            TimeSpan.FromMinutes(MaxContinuousHeaterOnDurationMinutes),
            () =>
            {
                if (!_switchEntities.WaterHeaterSwitch.IsOn() || _waterHeaterTimerService.LastTurnedOnDateTime != turnedOnAt)
                {
                    return;
                }

                var onDuration = DateTime.UtcNow - turnedOnAt;
                TurnHeaterOff(
                    $"the continuous runtime safeguard fired after {onDuration.TotalMinutes:F1} minutes");

                var message =
                    $"Water heater auto-turned off by safeguard after running for {onDuration.TotalMinutes:F1} minutes (max continuous on time: {MaxContinuousHeaterOnDurationMinutes} minutes). Turned on at {turnedOnAt:yyyy-MM-dd HH:mm:ss} UTC.";
                _logger.LogWarning(message);
                _notifyServices.Notify(message, "Water heater safety turn-off");
            });
    }

    private void ReevaluateHeater(string reason)
    {
        if (AnyShowerActive)
        {
            CancelScheduledTurnOff();
            EnsureHeaterOn($"{reason} and showering is active");
            return;
        }

        if (AnyBathroomOccupied)
        {
            CancelScheduledTurnOff();

            if (_switchEntities.WaterHeaterSwitch.IsOn())
            {
                _waterHeaterTimerService.ScheduledTurnOffDateTime = GetMaxContinuousTurnOffDateTime();
            }

            return;
        }

        if (!_switchEntities.WaterHeaterSwitch.IsOn())
        {
            _waterHeaterTimerService.ScheduledTurnOffDateTime = null;
            ResetHeatingSession();
            return;
        }

        if (!_currentHeatingSessionHadShower)
        {
            TurnHeaterOff($"{reason} and no shower was detected in this heating session");
            return;
        }

        SchedulePostShowerRecoveryTurnOff(reason);
    }

    private void EnsureHeaterOn(string reason)
    {
        CancelScheduledTurnOff();

        if (_switchEntities.WaterHeaterSwitch.IsOn())
        {
            _waterHeaterTimerService.ScheduledTurnOffDateTime = GetMaxContinuousTurnOffDateTime();
            return;
        }

        _logger.LogInformation("Turning on water heater because {Reason}", reason);
        _switchEntities.WaterHeaterSwitch.TurnOn();

        ResetHeatingSession();
        var turnedOnAt = DateTime.UtcNow;
        _waterHeaterTimerService.LastTurnedOnDateTime = turnedOnAt;
        _waterHeaterTimerService.ScheduledTurnOffDateTime =
            turnedOnAt.AddMinutes(MaxContinuousHeaterOnDurationMinutes);
        ScheduleMaxContinuousOnTurnOff(turnedOnAt);
    }

    private void TurnHeaterOff(string reason)
    {
        CancelScheduledTurnOff();
        _maxContinuousOnTurnOff?.Dispose();
        _maxContinuousOnTurnOff = null;
        _waterHeaterTimerService.ScheduledTurnOffDateTime = null;

        if (!_switchEntities.WaterHeaterSwitch.IsOn())
        {
            ResetHeatingSession();
            return;
        }

        _logger.LogInformation("Turning off water heater because {Reason}", reason);
        _switchEntities.WaterHeaterSwitch.TurnOff();
        ResetHeatingSession();
    }

    private void SchedulePostShowerRecoveryTurnOff(string reason)
    {
        CancelScheduledTurnOff();

        var recoveryMinutes =
            BasePostShowerRecoveryMinutes +
            (_currentHeatingSessionShowerDuration.TotalMinutes * AdditionalRecoveryPerShowerMinute);
        var turnOffDelay = TimeSpan.FromMinutes(recoveryMinutes);
        _waterHeaterTimerService.ScheduledTurnOffDateTime = DateTime.UtcNow.Add(turnOffDelay);

        _logger.LogInformation(
            "All bathrooms are clear. Keeping the water heater on for {Minutes:F0} minutes of recovery after {ShowerMinutes:F1} minutes of showering. Reason: {Reason}",
            turnOffDelay.TotalMinutes,
            _currentHeatingSessionShowerDuration.TotalMinutes,
            reason);

        _scheduledTurnOff = _scheduler.Schedule(
            turnOffDelay,
            () => TurnHeaterOff(
                $"the post-shower recovery timer elapsed after {turnOffDelay.TotalMinutes:F0} minutes"));
    }

    private void CancelScheduledTurnOff()
    {
        _scheduledTurnOff?.Dispose();
        _scheduledTurnOff = null;
    }

    private DateTime GetMaxContinuousTurnOffDateTime()
    {
        var turnedOnAt = _waterHeaterTimerService.LastTurnedOnDateTime ?? DateTime.UtcNow;
        return turnedOnAt.AddMinutes(MaxContinuousHeaterOnDurationMinutes);
    }

    private void ResetHeatingSession()
    {
        _currentHeatingSessionHadShower = false;
        _currentHeatingSessionShowerDuration = TimeSpan.Zero;
    }

    private bool AnyBathroomOccupied => _bathroomPresence.IsOn() || _masterBathroomPresence.IsOn();

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
