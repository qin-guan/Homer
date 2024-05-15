using System.Reactive.Linq;
using Homer.NetDaemon.Entities;
using Homer.NetDaemon.Presence;
using NetDaemon.AppModel;

namespace Homer.NetDaemon.Apps.Bathroom;

[NetDaemonApp]
public class BathroomPresence
{
    private WaspState WaspState = WaspState.NoWasp;

    public BathroomPresence(BinarySensorEntities binarySensorEntities, SwitchEntities switchEntities)
    {
        var motionSensors = new List<BinarySensorEntity>
        {
            binarySensorEntities.BathroomSinkMotionOccupancy,
            binarySensorEntities.BathroomDoorMotionOccupancy
        };

        var contactSensors = new List<BinarySensorEntity>
        {
            binarySensorEntities.BathroomContactSensorContact
        };

        motionSensors.Select(e => e.StateChanges()).Merge()
            .Subscribe(e =>
            {
                
            });
    }
}