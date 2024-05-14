using Homer.NetDaemon.Entities;
using NetDaemon.AppModel;

namespace Homer.NetDaemon.Apps.LivingRoom;

[NetDaemonApp]
public class LivingRoomLights
{
    public LivingRoomLights(ILogger<LivingRoomLights> logger, BinarySensorEntities bse)
    {
        bse.PresenceSensorFp2B4c4PresenceSensor1.StateChanges()
            .Subscribe(e => logger.LogInformation("motion in living room: {E}", e));
    }
}