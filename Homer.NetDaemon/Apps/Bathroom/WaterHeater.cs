using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Homer.NetDaemon.Entities;
using Homer.NetDaemon.Services;
using NetDaemon.AppModel;
using NetDaemon.Extensions.Scheduler;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace Homer.NetDaemon.Apps.Bathroom;

[NetDaemonApp]
public class WaterHeater
{
    private const int ShowerDetectionThresholdSeconds = 30;
    private const int MinHeaterOnDurationMinutes = 10;
    private const int MaxHeaterOnDurationMinutes = 18;
    private const int PeriodicCheckIntervalSeconds = 30;
    private const int ShowerEndGracePeriodSeconds = 10;
    private const int MaxContinuousHeaterOnDurationHours = 1;
    
    private readonly ILogger<WaterHeater> _logger;
    private readonly SwitchEntities _switchEntities;
    private readonly IScheduler _scheduler;
    private readonly BathroomStatusService _bathroomStatusService;
    private readonly WaterHeaterTimerService _waterHeaterTimerService;
    private readonly NotifyServices _notifyServices;
    private IDisposable? _scheduledTurnOff;
    private IDisposable? _maxContinuousOnTurnOff;
    
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
        
        // Setup monitoring for regular bathroom
        var bathroomPresence = inputBooleanEntities.BathroomPresence;
        var bathroomMotionSensors = new[]
        {
            motionSensors.BathroomTuyaPresencePresence,
            motionSensors.BathroomSinkMotionOccupancy
        };
        SetupBathroomMonitoring("Bathroom", bathroomPresence, bathroomMotionSensors, _bathroomState);
        
        // Setup monitoring for master bathroom
        var masterBathroomPresence = inputBooleanEntities.MasterBathroomPresence;
        var masterBathroomMotionSensors = new[]
        {
            motionSensors.MasterBathroomSinkMotionOccupancy,
            motionSensors.MasterBathroomToiletMotionOccupancy
        };
        SetupBathroomMonitoring("Master Bathroom", masterBathroomPresence, masterBathroomMotionSensors, _masterBathroomState);
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
                UpdateBathroomStatus(bathroomName, presence, state);
                state.PeriodicCheck?.Dispose();
                
                // Schedule periodic checks starting after initial delay
                var checkState = (name: bathroomName, presence, sensors: motionSensors, state);
                state.PeriodicCheck = _scheduler.SchedulePeriodic(
                    checkState,
                    TimeSpan.FromSeconds(PeriodicCheckIntervalSeconds),
                    s => CheckShoweringState(s.name, s.presence, s.sensors, s.state)
                );
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
            
            // Turn on heater (if not already on from other bathroom)
            var wasHeaterOn = _switchEntities.WaterHeaterSwitch.IsOn();
            _switchEntities.WaterHeaterSwitch.TurnOn();
            if (!wasHeaterOn)
            {
                var turnedOnAt = DateTime.UtcNow;
                _waterHeaterTimerService.LastTurnedOnDateTime = turnedOnAt;
                ScheduleMaxContinuousOnTurnOff(turnedOnAt);
            }
            
            // Cancel any scheduled turn-off since shower is active
            _scheduledTurnOff?.Dispose();
            _scheduledTurnOff = null;
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
        
        state.IsShoweringDetected = false;
        state.FirstMotionAfterShowerStart = null;
        state.ShowerStartTime = null;
        
        // Update status
        UpdateBathroomStatus(bathroomName, presence, state);
        
        // Check if any shower is still active in either bathroom
        if (_bathroomState.IsShoweringDetected || _masterBathroomState.IsShoweringDetected)
        {
            _logger.LogInformation("Another shower is still active - keeping water heater on");
            return;
        }
        
        // Use maximum duration to ensure adequate recovery when multiple bathrooms used
        var heaterDurationMinutes = MaxHeaterOnDurationMinutes;
        
        _logger.LogInformation(
            "All showers ended - scheduling water heater turn off in {HeaterMinutes} minutes", 
            heaterDurationMinutes);
        
        // Cancel any existing scheduled turn-off
        _scheduledTurnOff?.Dispose();
        
        // Schedule turn-off after calculated duration to allow heating for next user
        _scheduledTurnOff = _scheduler.Schedule(TimeSpan.FromMinutes(heaterDurationMinutes), () =>
        {
            _logger.LogInformation("Turning off water heater after {Minutes} minute heating period", heaterDurationMinutes);
            _switchEntities.WaterHeaterSwitch.TurnOff();
            _maxContinuousOnTurnOff?.Dispose();
            _maxContinuousOnTurnOff = null;
            _scheduledTurnOff = null;
        });
    }

    private void ScheduleMaxContinuousOnTurnOff(DateTime turnedOnAt)
    {
        _maxContinuousOnTurnOff?.Dispose();
        _maxContinuousOnTurnOff = _scheduler.Schedule(
            TimeSpan.FromHours(MaxContinuousHeaterOnDurationHours),
            () =>
            {
                if (!_switchEntities.WaterHeaterSwitch.IsOn() || _waterHeaterTimerService.LastTurnedOnDateTime != turnedOnAt)
                {
                    return;
                }

                var onDuration = DateTime.UtcNow - turnedOnAt;
                if (onDuration <= TimeSpan.FromHours(MaxContinuousHeaterOnDurationHours))
                {
                    return;
                }

                _switchEntities.WaterHeaterSwitch.TurnOff();
                _maxContinuousOnTurnOff = null;

                var message =
                    $"Water heater auto-turned off by safeguard after running for {onDuration.TotalMinutes:F1} minutes (max continuous on time: {MaxContinuousHeaterOnDurationHours} hour). Turned on at {turnedOnAt.ToLocalTime():yyyy-MM-dd HH:mm:ss}.";
                _logger.LogWarning(message);
                _notifyServices.Notify(message, "Water heater safety turn-off");
            });
    }
    
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
