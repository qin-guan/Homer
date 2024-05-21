using System.Diagnostics.Metrics;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Homer.NetDaemon.Apps.Remotes;
using Homer.NetDaemon.Entities;
using Homer.ServiceDefaults.Metrics;
using NetDaemon.AppModel;
using NetDaemon.HassModel.Entities;

namespace Homer.NetDaemon.Apps.LivingRoom;

[NetDaemonApp]
public class LivingRoomFan : IAsyncInitializable
{
    private readonly ILogger<LivingRoomFan> _logger;

    private readonly List<BinarySensorEntity> _triggerEntities;
    private readonly List<BinarySensorEntity> _presenceEntities;
    private readonly InputBooleanEntity _fan;
    private readonly NumericSensorEntity _temperatureSensor;

    private bool Presence => _presenceEntities.Any(e => e.IsOn());
    private bool TooCold => _temperatureSensor.State < 26;

    public LivingRoomFan(
        ILogger<LivingRoomFan> logger,
        IScheduler scheduler,
        IrRemoteLock irRemoteLock,
        BinarySensorEntities binarySensorEntities,
        SensorEntities sensorEntities,
        InputBooleanEntities inputBooleanEntities,
        RemoteEntities remoteEntities
    )
    {
        var eventsProcessedMeter = EntityMetrics.MeterInstance.CreateCounter<int>("homer.netdaemon.living_room_fan.events_processed");

        _logger = logger;

        _triggerEntities =
        [
            binarySensorEntities.PresenceSensorFp2B4c4PresenceSensor2,
            binarySensorEntities.PresenceSensorFp2B4c4PresenceSensor3,
        ];

        _presenceEntities =
        [
            binarySensorEntities.PresenceSensorFp2B4c4PresenceSensor2,
            binarySensorEntities.PresenceSensorFp2B4c4PresenceSensor3,
        ];

        _fan = inputBooleanEntities.LivingRoomFan;
        _temperatureSensor = sensorEntities.Daikinap16703InsideTemperature;

        var triggerObservables = _triggerEntities.Select(e => e.StateChanges()).Merge();
        var presenceObservables = _presenceEntities.Select(e => e.StateChanges()).Merge();

        _temperatureSensor.StateChanges()
            .Where(_ =>
            {
                eventsProcessedMeter.Add(1);
                return TooCold;
            })
            .Subscribe(_ => { _fan.TurnOff(); });

        triggerObservables
            .Where(_ =>
            {
                eventsProcessedMeter.Add(1);
                return _triggerEntities.Any(e => e.IsOn());
            })
            .Throttle(TimeSpan.FromSeconds(30), scheduler)
            .Where(_ => _triggerEntities.Any(e => e.IsOn()))
            .Subscribe(_ =>
            {
                if (TooCold) return;
                _fan.TurnOn();
            });

        presenceObservables
            .Where(_ =>
            {
                eventsProcessedMeter.Add(1);
                return !Presence;
            })
            .Throttle(TimeSpan.FromSeconds(60), scheduler)
            .Where(_ => !Presence)
            .Subscribe(_ => { _fan.TurnOff(); });
    }

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (!Presence)
        {
            _fan.TurnOff();
        }

        return Task.CompletedTask;
    }
}