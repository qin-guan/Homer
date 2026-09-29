using System.Reactive.Concurrency;
using Homer.NetDaemon.Apps.Core;
using Homer.NetDaemon.Entities;
using Homer.NetDaemon.Services;
using NetDaemon.AppModel;

namespace Homer.NetDaemon.Apps.Bathroom;

[NetDaemonApp]
public class BathroomShowerDetection : ShowerDetection
{
    public BathroomShowerDetection(
        ILogger<BathroomShowerDetection> logger,
        IScheduler scheduler,
        BathroomStatusService bathroomStatusService,
        InputBooleanEntities inputBooleanEntities,
        BinarySensorEntities binarySensorEntities
    ) : base(
        logger,
        scheduler,
        bathroomStatusService,
        BathroomId.Bathroom,
        inputBooleanEntities.BathroomPresence,
        [binarySensorEntities.BathroomMotionOccupancy]
    )
    {
    }
}
