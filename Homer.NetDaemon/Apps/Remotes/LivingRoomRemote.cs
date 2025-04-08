using Homer.NetDaemon.Entities;
using NetDaemon.AppModel;

namespace Homer.NetDaemon.Apps.Remotes;

[NetDaemonApp]
public class LivingRoomRemote(RemoteEntities remoteEntities) : IAsyncInitializable
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        _ = Task.Run(async () =>
        {
            while (await Channels.Channels.LivingRoomChannel.Reader.WaitToReadAsync(cancellationToken))
            {
                while (Channels.Channels.LivingRoomChannel.Reader.TryRead(out var item))
                {
                    switch (item)
                    {
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

                    await Task.Delay(TimeSpan.FromSeconds(4), cancellationToken);
                }
            }
        }, cancellationToken);
    }
}