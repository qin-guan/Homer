using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Homer.NetDaemon.Entities;
using Homer.NetDaemon.Helpers;
using Homer.ServiceDefaults.Metrics;
using NetDaemon.AppModel;
using NetDaemon.Extensions.Scheduler;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace Homer.NetDaemon.Apps.Kitchen;

[NetDaemonApp]
public class KitchenLights : IAsyncInitializable
{
    private readonly List<BinarySensorEntity> _triggerEntities;
    private readonly List<BinarySensorEntity> _presenceEntities;
    private readonly List<SwitchEntity> _lights;
    private readonly List<SwitchEntity> _nightLights;

    private bool Presence => _presenceEntities.Any(e => e.IsOn());
    private bool TriggerPresence => _triggerEntities.Any(e => e.IsOn());

    private static KitchenLightingPreference LightingPreference()
    {
        if (TimeHelpers.TimeNow > new TimeOnly(2, 0) && TimeHelpers.TimeNow < new TimeOnly(5, 30))
        {
            return KitchenLightingPreference.Disabled;
        }

        if (TimeHelpers.TimeNow < new TimeOnly(21, 30))
        {
            return KitchenLightingPreference.All;
        }

        return KitchenLightingPreference.NightLight;
    }

    private const string NightStartCron = "30 21 * * *";
    private const string NightEndCron = "20 6 * * *";

    public KitchenLights(
        ILogger<KitchenLights> logger,
        IScheduler scheduler,
        BinarySensorEntities binarySensorEntities,
        SwitchEntities switchEntities,
        SensorEntities sensorEntities
    )
    {
        var eventsProcessedMeter =
            EntityMetrics.MeterInstance.CreateCounter<int>("homer.netdaemon.kitchen_lights.events_processed");

        _triggerEntities =
        [
            binarySensorEntities.PresenceSensorFp2B4c4PresenceSensor6,
            binarySensorEntities.KitchenTuyaPresencePresence
        ];

        _presenceEntities =
        [
            binarySensorEntities.PresenceSensorFp2B4c4PresenceSensor6,
            binarySensorEntities.KitchenTuyaPresencePresence
        ];

        _lights =
        [
            switchEntities.KitchenLightsRight,
        ];

        _nightLights =
        [
            switchEntities.KitchenLightsLeft,
        ];

        var lightSensor = sensorEntities.KitchenTuyaPresenceIlluminanceLux;

        var triggerObservables = _triggerEntities.Select(e => e.StateChanges()).Merge();
        var presenceObservables = _presenceEntities.Select(e => e.StateChanges()).Merge().DistinctUntilChanged();

        scheduler.ScheduleCron(NightStartCron, () =>
        {
            if (!_lights.Any(e => e.IsOn())) return;

            _nightLights.TurnOn();
            _lights.TurnOff();
        });

        scheduler.ScheduleCron(NightEndCron, () =>
        {
            if (!_nightLights.Any(e => e.IsOn())) return;

            _lights.TurnOn();
            _nightLights.TurnOff();
        });

        triggerObservables
            .Where(e =>
            {
                eventsProcessedMeter.Add(1);
                return TriggerPresence;
            })
            .Subscribe(_ =>
            {
                if (lightSensor.State > 1100) return;

                switch (LightingPreference())
                {
                    case KitchenLightingPreference.All:
                    {
                        _lights.TurnOn();
                        break;
                    }
                    case KitchenLightingPreference.NightLight:
                    {
                        _nightLights.TurnOn();
                        break;
                    }
                    case KitchenLightingPreference.Disabled:
                    default:
                        break;
                }
            });

        presenceObservables
            .Where(_ =>
            {
                eventsProcessedMeter.Add(1);
                return !Presence;
            })
            .Throttle(TimeSpan.FromMinutes(1), scheduler)
            .Where(_ => !Presence)
            .Subscribe(_ =>
            {
                _lights.TurnOff();
                _nightLights.TurnOff();
            });
    }

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (Presence) return Task.CompletedTask;

        _lights.TurnOff();
        _nightLights.TurnOff();

        return Task.CompletedTask;
    }
}