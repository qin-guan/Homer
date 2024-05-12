using Homer.NetDaemon.Entities;
using NetDaemon.AppModel;

namespace Homer.NetDaemon.Apps.Bathroom;

[NetDaemonApp]
public class BathroomPresence
{
    public BathroomPresence(BinarySensorEntities bse)
    {
        var presence = new Presence.Presence(
            [bse.MotionSensor, bse.MotionSensor2],
            [bse.ContactSensor],
            () => { },
            () => { }
        );
    }
}