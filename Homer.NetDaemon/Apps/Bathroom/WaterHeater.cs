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
    private const int HeaterOnDurationMinutes = 15;
    private const int PeriodicCheckIntervalSeconds = 30;
    private const int ShowerEndGracePeriodSeconds = 10;
    
    private readonly ILogger<WaterHeater> _logger;
    private readonly SwitchEntities _switchEntities;
    private readonly IScheduler _scheduler;
    private IDisposable? _scheduledTurnOff;
    private IDisposable? _periodicCheck;
    private bool _isShoweringDetected = false;
    private DateTime? _lastMotionTime = null;
    private DateTime? _firstMotionAfterShowerStart = null;
    
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
        
        var bathroomPresence = inputBooleanEntities.BathroomPresence;
        var motionSensorsList = new[]
        {
            motionSensors.BathroomTuyaPresencePresence,
            motionSensors.BathroomSinkMotionOccupancy
        };
        
        // Track motion sensor changes
        Observable.Merge(motionSensorsList.Select(m => m.StateChanges()))
            .Where(e => e.New.IsOn())
            .Subscribe(_ => 
            {
                _lastMotionTime = DateTime.UtcNow;
                
                if (_isShoweringDetected)
                {
                    // Track first motion after shower starts for grace period
                    _firstMotionAfterShowerStart ??= DateTime.UtcNow;
                }
                
                CheckShoweringState(bathroomPresence, motionSensorsList);
            });
        
        // Monitor bathroom presence changes
        bathroomPresence.StateChanges().Subscribe(_ => 
        {
            if (bathroomPresence.IsOn())
            {
                // Presence detected, start periodic monitoring for shower
                _periodicCheck?.Dispose();
                
                // Schedule periodic checks starting after initial delay
                var checkState = (presence: bathroomPresence, sensors: motionSensorsList);
                _periodicCheck = _scheduler.SchedulePeriodic(
                    checkState,
                    TimeSpan.FromSeconds(PeriodicCheckIntervalSeconds),
                    state => CheckShoweringState(state.presence, state.sensors)
                );
            }
            else
            {
                // Presence ended, stop monitoring
                _periodicCheck?.Dispose();
                _periodicCheck = null;
                
                if (_isShoweringDetected)
                {
                    OnShowerEnded();
                }
            }
        });
    }
    
    private void CheckShoweringState(
        InputBooleanEntity bathroomPresence, 
        BinarySensorEntity[] motionSensors)
    {
        var presenceOn = bathroomPresence.IsOn();
        var anyMotion = motionSensors.Any(m => m.IsOn());
        
        // Calculate time since last motion (null means no motion detected yet)
        var timeSinceLastMotion = _lastMotionTime.HasValue 
            ? DateTime.UtcNow - _lastMotionTime.Value 
            : TimeSpan.MaxValue;
        
        // User is showering if:
        // 1. Presence is detected (person is in bathroom)
        // 2. No motion sensors are currently triggered
        // 3. It's been at least ShowerDetectionThresholdSeconds since last motion (gives time to enter shower)
        var isShowering = presenceOn && !anyMotion && timeSinceLastMotion > TimeSpan.FromSeconds(ShowerDetectionThresholdSeconds);
        
        if (isShowering && !_isShoweringDetected)
        {
            // Start of shower detected
            _logger.LogInformation("Shower detected - turning on water heater");
            _isShoweringDetected = true;
            _firstMotionAfterShowerStart = null;
            _switchEntities.WaterHeaterSwitch.TurnOn();
            
            // Cancel any scheduled turn-off
            _scheduledTurnOff?.Dispose();
            _scheduledTurnOff = null;
        }
        else if (_isShoweringDetected && anyMotion && _firstMotionAfterShowerStart.HasValue)
        {
            // Check if motion has been sustained for the grace period
            var timeSinceFirstMotion = DateTime.UtcNow - _firstMotionAfterShowerStart.Value;
            if (timeSinceFirstMotion > TimeSpan.FromSeconds(ShowerEndGracePeriodSeconds))
            {
                // Sustained motion detected - end of shower
                OnShowerEnded();
            }
        }
    }
    
    private void OnShowerEnded()
    {
        _logger.LogInformation("Shower ended - scheduling water heater turn off in {Minutes} minutes", HeaterOnDurationMinutes);
        _isShoweringDetected = false;
        _firstMotionAfterShowerStart = null;
        
        // Cancel any existing scheduled turn-off
        _scheduledTurnOff?.Dispose();
        
        // Schedule turn-off after HeaterOnDurationMinutes to allow heating for next user
        _scheduledTurnOff = _scheduler.Schedule(TimeSpan.FromMinutes(HeaterOnDurationMinutes), () =>
        {
            _logger.LogInformation("Turning off water heater after {Minutes} minute heating period", HeaterOnDurationMinutes);
            _switchEntities.WaterHeaterSwitch.TurnOff();
            _scheduledTurnOff = null;
        });
    }
}