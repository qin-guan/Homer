using System.Reactive.Concurrency;
using Homer.NetDaemon.Apps.Core;
using Homer.NetDaemon.Entities;
using Homer.NetDaemon.Services;
using NetDaemon.AppModel;

namespace Homer.NetDaemon.Apps.MasterBathroom;

[NetDaemonApp]
public class MasterBathroomShowerDetection : ShowerDetection
{
    public MasterBathroomShowerDetection(
        ILogger<MasterBathroomShowerDetection> logger,
        IScheduler scheduler,
        BathroomStatusService bathroomStatusService,
        InputBooleanEntities inputBooleanEntities,
        BinarySensorEntities binarySensorEntities
    ) : base(
        logger,
        scheduler,
        bathroomStatusService,
        BathroomId.MasterBathroom,
        inputBooleanEntities.MasterBathroomPresence,
        [
            binarySensorEntities.MasterBathroomSinkMotionOccupancy,
            binarySensorEntities.MasterBathroomToiletMotionOccupancy
        ]
    )
    {
    }
}
