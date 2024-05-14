using Homer.NetDaemon.Entities;
using NetDaemon.AppModel;

namespace Homer.NetDaemon.Apps.LivingRoom;

[NetDaemonApp]
public class LivingRoomLights
{
    public LivingRoomLights(
        ILogger<LivingRoomLights> logger, 
        InputBooleanEntities inputBooleanEntities,
        RemoteEntities remoteEntities
    )
    {
        inputBooleanEntities.LivingRoomFanLights.StateChanges()
            .Subscribe(_ => { remoteEntities.LivingRoomRemote.SendCommand("Light Power", "Living Room KDK"); });
    }
}