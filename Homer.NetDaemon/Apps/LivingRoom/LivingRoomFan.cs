using System.Reactive.Concurrency;
using Homer.NetDaemon.Entities;
using NetDaemon.AppModel;

namespace Homer.NetDaemon.Apps.LivingRoom;

[NetDaemonApp]
public class LivingRoomFan
{
    public LivingRoomFan(
        ILogger<LivingRoomFan> logger,
        IScheduler scheduler,
        InputBooleanEntities inputBooleanEntities,
        RemoteEntities remoteEntities
    )
    {
        inputBooleanEntities.LivingRoomFan.StateChanges()
            .Subscribe(_ => { remoteEntities.LivingRoomRemote.SendCommand("Power", "Living Room KDK"); });
    }
}