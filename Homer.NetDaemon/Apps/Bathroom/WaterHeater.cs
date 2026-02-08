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
    private const int InitialShowerCheckDelaySeconds = 45;
    private const int HeaterOnDurationMinutes = 15;
    private const int PeriodicCheckIntervalSeconds = 30;
    
    private readonly ILogger<WaterHeater> _logger;
    private readonly SwitchEntities _switchEntities;
    private readonly IScheduler _scheduler;
    private IDisposable? _scheduledTurnOff;
    private IDisposable? _showerDetectionTimer;
    private bool _isShoweringDetected = false;
    private DateTime _lastMotionTime = DateTime.Now;
    
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
                _lastMotionTime = DateTime.Now;
                CheckShoweringState(bathroomPresence, motionSensorsList);
            });
        
        // Monitor bathroom presence changes
        bathroomPresence.StateChanges().Subscribe(_ => 
        {
            if (bathroomPresence.IsOn())
            {
                // Presence detected, start monitoring for shower
                _showerDetectionTimer?.Dispose();
                _showerDetectionTimer = _scheduler.Schedule(TimeSpan.FromSeconds(InitialShowerCheckDelaySeconds), () =>
                {
                    CheckShoweringState(bathroomPresence, motionSensorsList);
                });
            }
            else
            {
                // Presence ended
                _showerDetectionTimer?.Dispose();
                _showerDetectionTimer = null;
                
                if (_isShoweringDetected)
                {
                    OnShowerEnded();
                }
            }
        });
        
        // Periodically check for showering state when presence is on
        _scheduler.SchedulePeriodic(TimeSpan.FromSeconds(PeriodicCheckIntervalSeconds), () =>
        {
            if (bathroomPresence.IsOn())
            {
                CheckShoweringState(bathroomPresence, motionSensorsList);
            }
        });
    }
    
    private void CheckShoweringState(
        InputBooleanEntity bathroomPresence, 
        BinarySensorEntity[] motionSensors)
    {
        var presenceOn = bathroomPresence.IsOn();
        var anyMotion = motionSensors.Any(m => m.IsOn());
        var timeSinceLastMotion = DateTime.Now - _lastMotionTime;
        
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
            _switchEntities.WaterHeaterSwitch.TurnOn();
            
            // Cancel any scheduled turn-off
            _scheduledTurnOff?.Dispose();
            _scheduledTurnOff = null;
        }
        else if (_isShoweringDetected && anyMotion)
        {
            // End of shower detected (motion resumed after showering)
            OnShowerEnded();
        }
    }
    
    private void OnShowerEnded()
    {
        _logger.LogInformation("Shower ended - scheduling water heater turn off in {Minutes} minutes", HeaterOnDurationMinutes);
        _isShoweringDetected = false;
        
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