using System.Reactive.Concurrency;
using Homer.NetDaemon.Entities;
using NetDaemon.AppModel;

namespace Homer.NetDaemon.Apps.Bathroom;

[NetDaemonApp]
public class WaterHeaterKeepAlive
{
    public WaterHeaterKeepAlive(SwitchEntities switchEntities, IScheduler scheduler)
    {
        scheduler.SchedulePeriodic(switchEntities, TimeSpan.FromMinutes(1),
            (se) => { se.WaterHeaterSwitchLedDisabledNight.Toggle(); });
    }
}