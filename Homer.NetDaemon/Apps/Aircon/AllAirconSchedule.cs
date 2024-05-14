using System.Reactive.Concurrency;
using Homer.NetDaemon.Entities;
using NetDaemon.AppModel;
using NetDaemon.Extensions.Scheduler;

namespace Homer.NetDaemon.Apps.Aircon;

[NetDaemonApp]
public class AllAirconSchedule
{
    public AllAirconSchedule(ClimateEntities ce, IScheduler scheduler)
    {
        scheduler.ScheduleCron("30 3 * * *", () =>
        {
            foreach (var aircon in ce.EnumerateAll())
            {
                if (aircon.EntityId == "climate.daikinap97235") continue;
                aircon.TurnOff();
            }
        });
    }
}