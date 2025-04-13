using System.Reactive.Concurrency;
using Homer.NetDaemon.Entities;
using NetDaemon.AppModel;
using NetDaemon.Extensions.Scheduler;

namespace Homer.NetDaemon.Apps.Bathroom;

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
            ndScheduler.RunIn(TimeSpan.FromMinutes(20), () => { switchEntities.WaterHeaterSwitch.TurnOff(); });
        });
        
        scheduler.ScheduleCron("30 20 * * *", () =>
        {
            switchEntities.WaterHeaterSwitch.TurnOn();
            ndScheduler.RunIn(TimeSpan.FromMinutes(10), () => { switchEntities.WaterHeaterSwitch.TurnOff(); });
        });
    }
}