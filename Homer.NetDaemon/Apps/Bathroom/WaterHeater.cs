using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Homer.NetDaemon.Entities;
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
    private const int MaxHeaterOnDurationMinutes = 20;
    private const int PeriodicCheckIntervalSeconds = 30;
    private const int ShowerEndGracePeriodSeconds = 10;
    
    private readonly ILogger<WaterHeater> _logger;
    private readonly SwitchEntities _switchEntities;
    private readonly IScheduler _scheduler;
    private IDisposable? _scheduledTurnOff;
    
    // Track state for each bathroom separately
    private readonly BathroomShowerState _bathroomState = new();
    private readonly BathroomShowerState _masterBathroomState = new();
    
    private class BathroomShowerState
    {
        public IDisposable? PeriodicCheck { get; set; }
        public bool IsShoweringDetected { get; set; }
        public DateTime? LastMotionTime { get; set; }
        public DateTime? FirstMotionAfterShowerStart { get; set; }
        public DateTime? ShowerStartTime { get; set; }
    }
    
    public WaterHeater(
        ILogger<WaterHeater> logger,
        SwitchEntities switchEntities, 
        BinarySensorEntities motionSensors,
        InputBooleanEntities inputBooleanEntities,
        IScheduler scheduler)
    {
        _logger = logger;
        _switchEntities = switchEntities;
        _scheduler = scheduler;
        
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
                // Presence detected, start periodic monitoring for shower
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
                    OnShowerEnded(bathroomName, state);
                }
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
            
            // Turn on heater (if not already on from other bathroom)
            _switchEntities.WaterHeaterSwitch.TurnOn();
            
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
                OnShowerEnded(bathroomName, state);
            }
        }
    }
    
    private void OnShowerEnded(string bathroomName, BathroomShowerState state)
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
        
        // Check if any shower is still active in either bathroom
        if (_bathroomState.IsShoweringDetected || _masterBathroomState.IsShoweringDetected)
        {
            _logger.LogInformation("Another shower is still active - keeping water heater on");
            return;
        }
        
        // Calculate adaptive heater duration based on all recent shower activity
        var heaterDurationMinutes = CalculateHeaterDurationForAllShowers();
        
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
            _scheduledTurnOff = null;
        });
    }
    
    private int CalculateHeaterDurationForAllShowers()
    {
        // When overlapping or sequential showers occur from both bathrooms,
        // use maximum duration to ensure adequate water heating recovery.
        // This conservative approach ensures hot water availability for next user(s).
        return MaxHeaterOnDurationMinutes;
    }
}