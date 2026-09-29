using System.Reactive.Linq;
using Homer.NetDaemon.Entities;
using Homer.ServiceDefaults.Metrics;
using NetDaemon.AppModel;

namespace Homer.NetDaemon.Apps.LivingRoom;

// [Focus]
[NetDaemonApp]
public class LivingRoomLightSwitch
{
    public LivingRoomLightSwitch(
        SensorEntities sensorEntities,
        SwitchEntities switchEntities,
        RemoteEntities remoteEntities,
        LightEntities lightEntities,
        EventEntities eventEntities
    )
    {

        eventEntities.LivingRoomLightsAction.StateChanges()
            .Where(e =>
            {
                EntityMetrics.AutomationEvent("living_room_light_switch");
                return e.Entity.Attributes?.EventType == "single_center";
            })
            .Subscribe(e => { lightEntities.LivingRoomKdk.Toggle(); });

        eventEntities.LivingRoomLightsAction.StateChanges()
            .Where(e =>
            {
                EntityMetrics.AutomationEvent("living_room_light_switch");
                return e.Entity.Attributes?.EventType == "double_center";
            })
            .Subscribe(e => { switchEntities.DiningTableLights.Toggle(); });
    }
}