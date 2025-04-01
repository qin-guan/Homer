using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Homer.NetDaemon.Entities;
using NetDaemon.AppModel;
using NetDaemon.Extensions.Scheduler;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace Homer.NetDaemon.Apps.Bathroom;

// [Focus]
[NetDaemonApp]
public class WaterHeater
{
    public WaterHeater(ClimateEntities climateEntities, SwitchEntities switchEntities, IScheduler scheduler)
    {
        switchEntities.WaterHeaterSwitch.StateChanges()
            .Where(s => s.New.IsOn())
            .WhenStateIsFor(s => s.IsOn(), TimeSpan.FromMinutes(30), scheduler)
            .Subscribe(_ =>
            {
                switchEntities.WaterHeaterSwitch.TurnOff();
            });
            
        scheduler.ScheduleCron("0 19 * * *", () =>
        {
            switchEntities.WaterHeaterSwitch.TurnOn();
        });
    }
}