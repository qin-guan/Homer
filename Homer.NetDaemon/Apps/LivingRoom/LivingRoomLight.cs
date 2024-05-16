using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Homer.NetDaemon.Apps.Remotes;
using Homer.NetDaemon.Entities;
using NetDaemon.AppModel;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace Homer.NetDaemon.Apps.LivingRoom;

[NetDaemonApp]
public class LivingRoomLight : IAsyncInitializable
{
    private readonly ILogger<LivingRoomLight> _logger;

    private readonly List<BinarySensorEntity> _triggerEntities;
    private readonly List<BinarySensorEntity> _presenceEntities;
    private readonly InputBooleanEntity _light;
    private readonly NumericSensorEntity _lightSensor;

    private bool Presence => _presenceEntities.Any(e => e.IsOn());
    private bool TooBright => _lightSensor.State > 50;
    private bool TooDark => _lightSensor.State < 10;

    public LivingRoomLight(
        ILogger<LivingRoomLight> logger,
        IScheduler scheduler,
        IrRemoteLock irRemoteLock,
        BinarySensorEntities binarySensorEntities,
        SensorEntities sensorEntities,
        InputBooleanEntities inputBooleanEntities,
        RemoteEntities remoteEntities
    )
    {
        _logger = logger;

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
        var presenceObservables = _presenceEntities.Select(e => e.StateChanges()).Merge();

        _lightSensor.StateChanges()
            .WhenStateIsFor(_ => TooBright, TimeSpan.FromMinutes(5), scheduler)
            .Subscribe(_ => { _light.TurnOff(); });

        _lightSensor.StateChanges()
            .WhenStateIsFor(_ => TooDark && Presence, TimeSpan.FromMinutes(5), scheduler)
            .Subscribe(_ => { _light.TurnOn(); });

        presenceObservables
            .Where(e => Presence)
            .Subscribe(_ => { _light.TurnOn(); });

        presenceObservables
            .WhenStateIsFor(e => !Presence, TimeSpan.FromMinutes(1), scheduler)
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