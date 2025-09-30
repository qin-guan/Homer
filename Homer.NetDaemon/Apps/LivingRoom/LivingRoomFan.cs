using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Homer.NetDaemon.Entities;
using Homer.ServiceDefaults.Metrics;
using NetDaemon.AppModel;
using NetDaemon.HassModel.Entities;

namespace Homer.NetDaemon.Apps.LivingRoom;

[NetDaemonApp]
public class LivingRoomFan : IAsyncInitializable
{
    private readonly ILogger<LivingRoomFan> _logger;

    private readonly List<BinarySensorEntity> _presenceEntities;
    private readonly FanEntity _fan;
    private readonly InputBooleanEntity _laundryMode;
    private readonly NumericSensorEntity _temperatureSensor;

    private bool Presence => _presenceEntities.Any(e => e.IsOn());
    private bool TooCold => _temperatureSensor.State < 26;
    private bool ManualOverride => _laundryMode.IsOn();

    public LivingRoomFan(
        ILogger<LivingRoomFan> logger,
        IScheduler scheduler,
        BinarySensorEntities binarySensorEntities,
        SensorEntities sensorEntities,
        InputBooleanEntities inputBooleanEntities,
        RemoteEntities remoteEntities,
        FanEntities fanEntities
    )
    {
        var eventsProcessedMeter =
            EntityMetrics.MeterInstance.CreateCounter<int>("homer.netdaemon.living_room_fan.events_processed");

        _logger = logger;

        _laundryMode = inputBooleanEntities.LiangYiMoShi;

        List<BinarySensorEntity> triggerEntities = [
            binarySensorEntities.PresenceSensorFp2B4c4PresenceSensor2,
            binarySensorEntities.PresenceSensorFp2B4c4PresenceSensor3,
        ];

        _presenceEntities =
        [
            binarySensorEntities.PresenceSensorFp2B4c4PresenceSensor2,
            binarySensorEntities.PresenceSensorFp2B4c4PresenceSensor3,
        ];

        _fan = fanEntities.LivingRoomKdk;
        _temperatureSensor = sensorEntities.Daikinap16703InsideTemperature;

        var triggerObservables = triggerEntities.Select(e => e.StateChanges()).Merge();
        var presenceObservables = _presenceEntities.Select(e => e.StateChanges()).Merge();

        _temperatureSensor.StateChanges()
            .Where(_ => !ManualOverride)
            .Where(_ =>
            {
                eventsProcessedMeter.Add(1);
                return TooCold;
            })
            .Subscribe(_ => { _fan.TurnOff(); });

        triggerObservables
            .Where(_ => !ManualOverride)
            .Where(_ =>
            {
                eventsProcessedMeter.Add(1);
                return triggerEntities.Any(e => e.IsOn());
            })
            .Throttle(TimeSpan.FromSeconds(10), scheduler)
            .Where(_ => triggerEntities.Any(e => e.IsOn()))
            .Subscribe(_ =>
            {
                if (TooCold) return;
                _fan.TurnOn();
            });

        presenceObservables
            .Where(_ => !ManualOverride)
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