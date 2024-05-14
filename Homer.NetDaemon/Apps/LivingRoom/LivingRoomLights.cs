using Homer.NetDaemon.Entities;
using NetDaemon.AppModel;

namespace Homer.NetDaemon.Apps.LivingRoom;

[NetDaemonApp]
public class LivingRoomLights
{
    public LivingRoomLights(ILogger<LivingRoomLights> logger, BinarySensorEntities bse)
    {
    }
}