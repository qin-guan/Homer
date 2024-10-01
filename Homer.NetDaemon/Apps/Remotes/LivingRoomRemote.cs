using Homer.NetDaemon.Entities;
using NetDaemon.AppModel;

namespace Homer.NetDaemon.Apps.Remotes;

[NetDaemonApp]
public class LivingRoomRemote(IrRemoteChannel irRemoteChannel, RemoteEntities remoteEntities) : IAsyncInitializable
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        while (await irRemoteChannel.LivingRoomChannel.Reader.WaitToReadAsync(cancellationToken))
        {
            while (irRemoteChannel.LivingRoomChannel.Reader.TryRead(out var item))
            {
                switch (item)
                {
                    case LivingRoomRemoteCommand.Fan:
                    {
                        remoteEntities.LivingRoomRemote.SendCommand("Power", "Living Room KDK");
                        break;
                    }
                    case LivingRoomRemoteCommand.Light:
                    {
                        remoteEntities.LivingRoomRemote.SendCommand("Light Power", "Living Room KDK");
                        break;
                    }
                    case LivingRoomRemoteCommand.Dyson:
                    {
                        remoteEntities.LivingRoomRemote.SendCommand("Power", "Dyson");
                        break;
                    }
                    default:
                    {
                        throw new Exception("Unhandled command.");
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
        }
    }
}