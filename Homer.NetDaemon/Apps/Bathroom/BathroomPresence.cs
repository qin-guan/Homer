using Homer.NetDaemon.Entities;
using NetDaemon.AppModel;

namespace Homer.NetDaemon.Apps.Bathroom;

[NetDaemonApp]
public class BathroomPresence
{
    public BathroomPresence(BinarySensorEntities bse, SwitchEntities se)
    {
        var presence = new Presence.Presence(
            [bse.BathroomDoorMotionOccupancy, bse.BathroomSinkMotionOccupancy],
            [bse.BathroomDoorMotionOccupancy],
            () =>
            {
                // se.BathroomLightsCenter.TurnOn();
            },
            () =>
            {
                // se.BathroomLightsCenter.TurnOff();
            }
        );
    }
}