using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Homer.NetDaemon.Apps.Core;
using Homer.NetDaemon.Entities;
using NetDaemon.AppModel;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace Homer.NetDaemon.Apps.Bathroom;

[NetDaemonApp]
public class BathroomPresence : Occupancy
{
    public BathroomPresence(
        IScheduler scheduler,
        InputDatetimeEntities inputDatetimeEntities,
        InputBooleanEntities inputBooleanEntities,
        BinarySensorEntities contactSensors,
        BinarySensorEntities motionSensors,
        SwitchEntities switchEntities
    ) : base(
        inputDatetimeEntities.BathroomLastPresence,
        inputBooleanEntities.BathroomPresence,
        [contactSensors.BathroomContactSensorContact],
        [motionSensors.BathroomDoorMotionOccupancy, motionSensors.BathroomSinkMotionOccupancy]
    )
    {
        inputBooleanEntities.BathroomPresence.StateChanges()
            .Where(e => e.Entity.IsOn())
            .Subscribe(_ => { switchEntities.BathroomLightsCenter.TurnOn(); });

        inputBooleanEntities.BathroomPresence.StateChanges()
            .WhenStateIsFor(e => e.IsOff(), TimeSpan.FromSeconds(45), scheduler)
            .Subscribe(_ => { switchEntities.BathroomLightsCenter.TurnOff(); });
    }
}