using System.Reactive.Linq;
using Homer.NetDaemon.Entities;
using Homer.ServiceDefaults.Metrics;
using NetDaemon.AppModel;

namespace Homer.NetDaemon.Apps.LivingRoom;

[NetDaemonApp]
public class LivingRoomLightSwitch
{
    public LivingRoomLightSwitch(
        SensorEntities sensorEntities,
        SwitchEntities switchEntities,
        RemoteEntities remoteEntities
    )
    {
        var eventsProcessedMeter =
            EntityMetrics.MeterInstance.CreateCounter<int>("living_room_light_switch.events_processed");

        sensorEntities.LivingRoomLightsAction.StateChanges()
            .Where(e =>
            {
                eventsProcessedMeter.Add(1);
                return e.Entity.State == "single_center";
            })
            .Subscribe(e => { remoteEntities.LivingRoomRemote.SendCommand("Light Power", "Living Room KDK"); });

        sensorEntities.LivingRoomLightsAction.StateChanges()
            .Where(e =>
            {
                eventsProcessedMeter.Add(1);
                return e.Entity.State == "double_center";
            })
            .Subscribe(e => { switchEntities.DiningTableLights.Toggle(); });
    }
}