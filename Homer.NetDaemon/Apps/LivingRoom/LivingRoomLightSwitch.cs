using System.Reactive.Linq;
using Homer.NetDaemon.Entities;
using NetDaemon.AppModel;

namespace Homer.NetDaemon.Apps.LivingRoom;

[NetDaemonApp]
[Focus]
public class LivingRoomLightSwitch
{
    public LivingRoomLightSwitch(SensorEntities sensorEntities, SwitchEntities switchEntities)
    {
        sensorEntities.LivingRoomLightsAction.StateChanges()
            .Where(e => e.Entity.State == "single_center")
            .Subscribe(e =>
            {
                switchEntities.LivingRoomLightsCenter.Toggle();
            });
        
        sensorEntities.LivingRoomLightsAction.StateChanges()
            .Where(e => e.Entity.State == "double_center")
            .Subscribe(e =>
            {
                switchEntities.DiningTableLights.Toggle();
            });
    }
}