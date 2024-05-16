using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Homer.NetDaemon.Apps.Remotes;
using Homer.NetDaemon.Entities;
using NetDaemon.AppModel;
using NetDaemon.HassModel;
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

        var triggerObservables = _triggerEntities.Select(e => e.StateChanges()).Merge().DistinctUntilChanged();
        var presenceObservables = _presenceEntities.Select(e => e.StateChanges()).Merge().DistinctUntilChanged();

        _temperatureSensor.StateChanges()
            .Where(_ => TooCold)
            .Subscribe(_ => { _fan.TurnOff(); });

        triggerObservables
            .WhenStateIsFor(e => e.IsOn(), TimeSpan.FromSeconds(15), scheduler)
            .Subscribe(_ =>
            {
                if (TooCold) return;
                _fan.TurnOn();
            });

        presenceObservables
            .WhenStateIsFor(e => e.IsOff(), TimeSpan.FromMinutes(2), scheduler)
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