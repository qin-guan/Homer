using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Homer.NetDaemon.Apps.Core;
using Homer.NetDaemon.Entities;
using NetDaemon.AppModel;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace Homer.NetDaemon.Apps.Bedroom4;

[NetDaemonApp]
public class Bedroom4Lights : Occupancy
{
    public Bedroom4Lights(
        IScheduler scheduler,
        InputDatetimeEntities inputDatetimeEntities,
        InputBooleanEntities inputBooleanEntities,
        BinarySensorEntities contactSensors,
        BinarySensorEntities motionSensors,
        SwitchEntities switchEntities
    ) : base(
        inputDatetimeEntities.Bedroom4LastPresence,
        inputBooleanEntities.Bedroom4Presence,
        [contactSensors.Bedroom4DoorContact],
        [motionSensors.ScreekHumanSensor2a06ead0Zone1Presence],
        TimeSpan.FromSeconds(5)
    )
    {
        inputBooleanEntities.Bedroom4Presence.StateChanges()
            .Where(e => e.Entity.IsOn())
            .Subscribe(_ => { switchEntities.Bedroom4Lights.TurnOn(); });

        inputBooleanEntities.Bedroom4Presence.StateChanges()
            .WhenStateIsFor(e => e.IsOff(), TimeSpan.FromMinutes(1), scheduler)
            .Subscribe(_ => { switchEntities.Bedroom4Lights.TurnOff(); });
    }
}