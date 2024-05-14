using System.Reactive.Concurrency;
using Homer.NetDaemon.Entities;
using NetDaemon.AppModel;
using NetDaemon.Extensions.Scheduler;

namespace Homer.NetDaemon.Apps.Aircon;

[NetDaemonApp]
public class Bedroom4Schedule
{
    public Bedroom4Schedule(ClimateEntities ce, IScheduler scheduler)
    {
        scheduler.ScheduleCron("0 5 * * *", () =>
        {
            ce.Daikinap97235.TurnOff();
        });
    }
}