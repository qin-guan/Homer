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
        RemoteEntities remoteEntities,
        InputBooleanEntities inputBooleanEntities
    )
    {
        var eventsProcessedMeter =
            EntityMetrics.MeterInstance.CreateCounter<int>("homer.netdaemon.living_room_light_switch.events_processed");

        sensorEntities.LivingRoomLightsAction.StateChanges()
            .Where(e =>
            {
                eventsProcessedMeter.Add(1);
                return e.Entity.State == "single_center";
            })
            .Subscribe(e => { inputBooleanEntities.LivingRoomFanLights.Toggle(); });

        sensorEntities.LivingRoomLightsAction.StateChanges()
            .Where(e =>
            {
                eventsProcessedMeter.Add(1);
                return e.Entity.State == "double_center";
            })
            .Subscribe(e => { switchEntities.DiningTableLights.Toggle(); });
    }
}