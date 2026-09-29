using Homer.NetDaemon.Apps.Core;
using Homer.NetDaemon.Entities;
using NetDaemon.AppModel;

namespace Homer.NetDaemon.Apps.MasterBathroom;

[NetDaemonApp]
public class MasterBathroomPresence : Occupancy
{
    public MasterBathroomPresence(
        InputDatetimeEntities inputDatetimeEntities,
        InputBooleanEntities inputBooleanEntities,
        BinarySensorEntities binarySensorEntities
    ) : base(
        inputDatetimeEntities.MasterBathroomLastPresence,
        inputBooleanEntities.MasterBathroomPresence,
        [binarySensorEntities.MasterBathroomDoorContact],
        [binarySensorEntities.MasterBathroomSinkMotionOccupancy, binarySensorEntities.MasterBathroomToiletMotionOccupancy],
        TimeSpan.FromSeconds(12)
    )
    {
    }
}
