using System.Reactive.Concurrency;
using Homer.NetDaemon.Entities;
using NetDaemon.AppModel;
using NetDaemon.Extensions.Scheduler;

namespace Homer.NetDaemon.Apps.Bathroom;

[Focus]
[NetDaemonApp]
public class WaterHeater
{
    public WaterHeater(ClimateEntities climateEntities, SwitchEntities switchEntities, EventEntities eventEntities,
        TtsEntities ttsEntities, MediaPlayerEntities mediaPlayerEntities, IScheduler scheduler,
        INetDaemonScheduler ndScheduler)
    {
        scheduler.ScheduleCron("0 19 * * *", () =>
        {
            switchEntities.WaterHeaterSwitch.TurnOn();
            ndScheduler.RunIn(TimeSpan.FromMinutes(30), () => { switchEntities.WaterHeaterSwitch.TurnOff(); });
        });
    }
}