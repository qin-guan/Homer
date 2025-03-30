using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Homer.NetDaemon.Entities;
using Homer.ServiceDefaults.Metrics;
using NetDaemon.AppModel;
using NetDaemon.HassModel.Entities;

namespace Homer.NetDaemon.Apps.LivingRoom;

[NetDaemonApp]
public class LivingRoomLight : IAsyncInitializable
{
    private readonly ILogger<LivingRoomLight> _logger;

    private readonly List<BinarySensorEntity> _triggerEntities;
    private readonly List<BinarySensorEntity> _presenceEntities;
    private readonly InputBooleanEntity _light;
    private readonly InputBooleanEntity _laundryMode;
    private readonly NumericSensorEntity _lightSensor;

    private bool Presence => _presenceEntities.Any(e => e.IsOn());
    private bool WithinTooBrightRange => _lightSensor.State is > 50 and < 55;
    private bool TooBright => _lightSensor.State > 40;
    private bool TooDark => _lightSensor.State < 10;
    private bool ManualOverride => _laundryMode.IsOn();

    public LivingRoomLight(
        ILogger<LivingRoomLight> logger,
        IScheduler scheduler,
        BinarySensorEntities binarySensorEntities,
        SensorEntities sensorEntities,
        InputBooleanEntities inputBooleanEntities,
        RemoteEntities remoteEntities
    )
    {
        var eventsProcessedMeter =
            EntityMetrics.MeterInstance.CreateCounter<int>("homer.netdaemon.living_room_light.events_processed");

        _logger = logger;

        _laundryMode = inputBooleanEntities.LiangYiMoShi;

        _triggerEntities =
        [
            binarySensorEntities.PresenceSensorFp2B4c4PresenceSensor2,
            binarySensorEntities.PresenceSensorFp2B4c4PresenceSensor3,
            binarySensorEntities.PresenceSensorFp2B4c4PresenceSensor4,
            binarySensorEntities.PresenceSensorFp2B4c4PresenceSensor5,
            binarySensorEntities.PresenceSensorFp2B4c4PresenceSensor7,
            binarySensorEntities.PresenceSensorFp2B4c4PresenceSensor8,
        ];

        _presenceEntities =
        [
            binarySensorEntities.ScreekHumanSensor2a872668AnyPresence,
            binarySensorEntities.PresenceSensorFp2B4c4PresenceSensor2,
            binarySensorEntities.PresenceSensorFp2B4c4PresenceSensor3,
            binarySensorEntities.PresenceSensorFp2B4c4PresenceSensor4,
            binarySensorEntities.PresenceSensorFp2B4c4PresenceSensor5,
            binarySensorEntities.PresenceSensorFp2B4c4PresenceSensor7,
            binarySensorEntities.PresenceSensorFp2B4c4PresenceSensor8,
        ];

        _light = inputBooleanEntities.LivingRoomFanLights;
        _lightSensor = sensorEntities.PresenceSensorFp2B4c4LightSensorLightLevel;

        var triggerObservables = _triggerEntities.Select(e => e.StateChanges()).Merge();
        var presenceObservables = _presenceEntities.Select(e => e.StateChanges()).Merge().DistinctUntilChanged();

        _lightSensor.StateChanges()
            .Where(_ => !ManualOverride)
            .Where(_ =>
            {
                eventsProcessedMeter.Add(1);
                return WithinTooBrightRange;
            })
            .Throttle(TimeSpan.FromMinutes(5), scheduler)
            .Where(_ => WithinTooBrightRange)
            .Subscribe(_ => { _light.TurnOff(); });

        _lightSensor.StateChanges()
            .Where(_ => !ManualOverride)
            .Where(_ =>
            {
                eventsProcessedMeter.Add(1);
                return TooDark && Presence;
            })
            .Throttle(TimeSpan.FromMinutes(5), scheduler)
            .Where(_ => TooDark && Presence)
            .Subscribe(_ => { _light.TurnOn(); });

        triggerObservables
            .Where(_ => !ManualOverride)
            .Where(e =>
            {
                eventsProcessedMeter.Add(1);
                return Presence;
            })
            .Subscribe(_ =>
            {
                if (!TooBright) _light.TurnOn();
            });

        presenceObservables
            .Where(_ => !ManualOverride)
            .Where(_ =>
            {
                eventsProcessedMeter.Add(1);
                return !Presence;
            })
            .Throttle(TimeSpan.FromMinutes(3), scheduler)
            .Where(e => !Presence)
            .Subscribe(_ => { _light.TurnOff(); });
    }

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (!Presence)
        {
            _light.TurnOff();
        }

        return Task.CompletedTask;
    }
}
