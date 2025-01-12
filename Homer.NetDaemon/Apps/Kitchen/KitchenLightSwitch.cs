using System.Reactive.Linq;
using Homer.NetDaemon.Entities;
using Homer.ServiceDefaults.Metrics;
using NetDaemon.AppModel;

namespace Homer.NetDaemon.Apps.Kitchen;

[NetDaemonApp]
public class KitchenLightSwitch
{
    public KitchenLightSwitch(
        SensorEntities sensorEntities,
        SwitchEntities switchEntities,
        RemoteEntities remoteEntities,
        EventEntities eventEntities
    )
    {
        var eventsProcessedMeter =
            EntityMetrics.MeterInstance.CreateCounter<int>("homer.netdaemon.kitchen_light_switch.events_processed");

        eventEntities.KitchenLightsAction.StateChanges()
            .Where(e =>
            {
                eventsProcessedMeter.Add(1);
                return e.Entity.Attributes?.EventType == "double_left";
            })
            .Subscribe(e => { switchEntities.DiningTableLights.Toggle(); });

        eventEntities.KitchenLightsAction.StateChanges()
            .Where(e =>
            {
                eventsProcessedMeter.Add(1);
                return e.Entity.Attributes?.EventType == "single_left";
            })
            .Subscribe(e => { switchEntities.KitchenLightsLeft.Toggle(); });
    }
}