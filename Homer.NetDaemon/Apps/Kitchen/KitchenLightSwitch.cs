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

        eventEntities.KitchenLightsAction.StateChanges()
            .Where(e =>
            {
                EntityMetrics.AutomationEvent("kitchen_light_switch");
                return e.Entity.Attributes?.EventType == "double_left";
            })
            .Subscribe(e => { switchEntities.DiningTableLights.Toggle(); });

        eventEntities.KitchenLightsAction.StateChanges()
            .Where(e =>
            {
                EntityMetrics.AutomationEvent("kitchen_light_switch");
                return e.Entity.Attributes?.EventType == "single_left";
            })
            .Subscribe(e => { switchEntities.KitchenLightsLeft.Toggle(); });
    }
}