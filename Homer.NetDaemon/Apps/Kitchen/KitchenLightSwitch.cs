using System.Reactive.Linq;
using Homer.NetDaemon.Entities;
using Homer.ServiceDefaults.Metrics;
using NetDaemon.AppModel;

namespace Homer.NetDaemon.Apps.Kitchen;

[Focus]
[NetDaemonApp]
public class KitchenLightSwitch
{
    public KitchenLightSwitch(
        SensorEntities sensorEntities,
        SwitchEntities switchEntities,
        RemoteEntities remoteEntities
    )
    {
        var eventsProcessedMeter =
            EntityMetrics.MeterInstance.CreateCounter<int>("homer.netdaemon.kitchen_light_switch.events_processed");

        sensorEntities.KitchenLightsAction.StateChanges()
            .Where(e =>
            {
                eventsProcessedMeter.Add(1);
                return e.Entity.State == "double_left";
            })
            .Subscribe(e => { switchEntities.DiningTableLights.Toggle(); });

        sensorEntities.KitchenLightsAction.StateChanges()
            .Where(e =>
            {
                eventsProcessedMeter.Add(1);
                return e.Entity.State == "single_left";
            })
            .Subscribe(e => { switchEntities.KitchenLightsLeft.Toggle(); });
    }
}