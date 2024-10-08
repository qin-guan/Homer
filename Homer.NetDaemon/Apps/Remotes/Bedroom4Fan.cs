using Homer.NetDaemon.Entities;
using NetDaemon.AppModel;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace Homer.NetDaemon.Apps.Remotes;

[NetDaemonApp]
public class Bedroom4Fan
{
    public Bedroom4Fan(
        FanEntities fanEntities,
        RemoteEntities remoteEntities
    )
    {
        fanEntities.Bedroom4Fan.StateAllChanges()
            .SubscribeAsync((e) =>
            {
                var fanSpeed = (fanEntities.Bedroom4Fan.Attributes?.Percentage ?? 0) switch
                {
                    <= 16D => 1,
                    <= 33D => 2,
                    <= 50D => 3,
                    <= 66D => 4,
                    <= 83D => 5,
                    <= 100D => 6,
                    _ => 0
                };

                if (e.Entity.IsOn())
                {
                    remoteEntities.Bedroom4Remote.SendCommand($"Fan {fanSpeed}", "Bedroom 4 Fanco");
                }

                if (e.Entity.IsOff())
                {
                    remoteEntities.Bedroom4Remote.SendCommand("Power", "Bedroom 4 Fanco");
                }

                return Task.CompletedTask;
            });
    }
}