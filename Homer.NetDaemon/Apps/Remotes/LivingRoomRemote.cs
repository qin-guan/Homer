using Homer.NetDaemon.Entities;

namespace Homer.NetDaemon.Apps.Remotes;

public class LivingRoomRemote(IServiceProvider serviceProvider) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var remoteEntities = scope.ServiceProvider.GetRequiredService<RemoteEntities>();

        await foreach (var item in Channels.Channels.LivingRoomChannel.Reader.ReadAllAsync(stoppingToken))
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

            await Task.Delay(TimeSpan.FromSeconds(4), stoppingToken);
        }
    }
}