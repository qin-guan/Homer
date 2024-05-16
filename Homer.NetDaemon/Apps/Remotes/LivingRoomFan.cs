using Homer.NetDaemon.Entities;
using NetDaemon.AppModel;
using NetDaemon.HassModel;

namespace Homer.NetDaemon.Apps.Remotes;

[NetDaemonApp]
public class LivingRoomFan
{
    public LivingRoomFan(
        IrRemoteLock irRemoteLock,
        InputBooleanEntities inputBooleanEntities,
        RemoteEntities remoteEntities
    )
    {
        inputBooleanEntities.LivingRoomFan.StateChanges()
            .SubscribeAsync(async _ =>
            {
                await irRemoteLock.SemaphoreSlim.WaitAsync();
                await Task.Delay(1000);
                remoteEntities.LivingRoomRemote.SendCommand("Power", "Living Room KDK");
                await Task.Delay(1000);
                irRemoteLock.SemaphoreSlim.Release();
            });
    }
}