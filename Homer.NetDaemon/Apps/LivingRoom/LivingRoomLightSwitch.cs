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
        var eventsProcessedMeter =
            EntityMetrics.MeterInstance.CreateCounter<int>("homer.netdaemon.living_room_light_switch.events_processed");

        eventEntities.LivingRoomLightsAction.StateChanges()
            .Where(e =>
            {
                eventsProcessedMeter.Add(1);
                return e.Entity.Attributes?.EventType == "single_center";
            })
            .Subscribe(e => { lightEntities.LivingRoomKdk.Toggle(); });

        eventEntities.LivingRoomLightsAction.StateChanges()
            .Where(e =>
            {
                eventsProcessedMeter.Add(1);
                return e.Entity.Attributes?.EventType == "double_center";
            })
            .Subscribe(e => { switchEntities.DiningTableLights.Toggle(); });
    }
}