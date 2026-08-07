using System.Reactive.Concurrency;
using Homer.NetDaemon.Entities;
using NetDaemon.AppModel;
using NetDaemon.Extensions.Scheduler;

namespace Homer.NetDaemon.Apps.Aircon;

[NetDaemonApp]
public class Bedroom3Schedule
{
    public Bedroom3Schedule(ClimateEntities ce, IScheduler scheduler)
    {
        scheduler.ScheduleCron("0 6 * * *", () =>
        {
            ce.Daikinap25067.TurnOff();
        });
    }
}
