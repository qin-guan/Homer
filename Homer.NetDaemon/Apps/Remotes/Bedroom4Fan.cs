using System.Diagnostics;
using Homer.NetDaemon.Entities;
using NetDaemon.AppModel;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace Homer.NetDaemon.Apps.Remotes;

[NetDaemonApp]
public class Bedroom4Fan
{
    public Bedroom4Fan(
        InputNumberEntities inputNumberEntities,
        InputBooleanEntities inputBooleanEntities,
        RemoteEntities remoteEntities
    )
    {
        inputNumberEntities.Bedroom4FanSpeed.StateAllChanges()
            .SubscribeAsync((e) =>
            {
                var fanSpeed = e.New?.State switch
                {
                    <= 16D => 1,
                    <= 33D => 2,
                    <= 50D => 3,
                    <= 66D => 4,
                    <= 83D => 5,
                    <= 100D => 6,
                    _ => 0
                };

                if (inputBooleanEntities.Bedroom4Fan.IsOn())
                {
                    remoteEntities.Bedroom4Remote.SendCommand($"Fan {fanSpeed}", "Bedroom 4 Fanco");
                }

                return Task.CompletedTask;
            });

        inputBooleanEntities.Bedroom4Fan.StateAllChanges()
            .SubscribeAsync((e) =>
            {
                if (e.Entity.IsOn())
                {
                    var fanSpeed = inputNumberEntities.Bedroom4FanSpeed.State switch
                    {
                        <= 16D => 1,
                        <= 33D => 2,
                        <= 50D => 3,
                        <= 66D => 4,
                        <= 83D => 5,
                        <= 100D => 6,
                        _ => 0
                    };

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