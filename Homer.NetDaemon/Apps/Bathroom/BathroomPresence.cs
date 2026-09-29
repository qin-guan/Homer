using Homer.NetDaemon.Apps.Core;
using Homer.NetDaemon.Entities;
using NetDaemon.AppModel;

namespace Homer.NetDaemon.Apps.Bathroom;

[NetDaemonApp]
public class BathroomPresence : Occupancy
{
    public BathroomPresence(
        InputDatetimeEntities inputDatetimeEntities,
        InputBooleanEntities inputBooleanEntities,
        BinarySensorEntities binarySensorEntities
    ) : base(
        inputDatetimeEntities.BathroomLastPresence,
        inputBooleanEntities.BathroomPresence,
        [binarySensorEntities.BathroomDoorContact],
        [binarySensorEntities.BathroomMotionOccupancy],
        TimeSpan.FromSeconds(30)
    )
    {
    }
}
