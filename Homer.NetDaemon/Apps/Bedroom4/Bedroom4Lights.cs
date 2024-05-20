using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Homer.NetDaemon.Entities;
using NetDaemon.AppModel;

namespace Homer.NetDaemon.Apps.Bedroom4;

[NetDaemonApp]
public class Bedroom4Lights
{
    public Bedroom4Lights(
        IScheduler scheduler,
        SensorEntities sensorEntities,
        SwitchEntities switchEntities,
        ClimateEntities climateEntities
    )
    {
        sensorEntities.ScreekHumanSensor2a06ead0Zone1TargetCounts.StateChanges()
            .Where(e => e.Entity.State > 0)
            .Subscribe(_ => { switchEntities.Bedroom4Lights.TurnOn(); });
        //
        // sensorEntities.ScreekHumanSensor2a06ead0Zone1TargetCounts.StateChanges()
        //     .Where(e => e.Entity.State == 0)
        //     .Subscribe(_ => { switchEntities.Bedroom4Lights.TurnOff(); });
    }
}