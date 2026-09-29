using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Homer.NetDaemon.Entities;
using Homer.ServiceDefaults.Metrics;
using NetDaemon.AppModel;
using NetDaemon.HassModel.Entities;

namespace Homer.NetDaemon.Apps.MasterBathroom;

[NetDaemonApp]
public class MasterBathroomPresenceMetrics
{
    public MasterBathroomPresenceMetrics(
        ILogger<MasterBathroomPresenceMetrics> logger,
        InputBooleanEntities inputBooleanEntities,
        BinarySensorEntities binarySensorEntities,
        IScheduler scheduler
    )
    {
        var sensorPresence = new List<BinarySensorEntity>
        {
            binarySensorEntities.MasterBathroomSinkMotionOccupancy,
            binarySensorEntities.MasterBathroomToiletMotionOccupancy,
        };

        var actualPresence = inputBooleanEntities.MasterBathroomPresence;
        DateTime? timing = null;
        var room = new KeyValuePair<string, object?>("room", "master_bathroom");

        sensorPresence.StateChanges()
            .Where(_ => sensorPresence.All(s => s.IsOff()) && actualPresence.IsOn() && timing is null)
            .Subscribe(_ =>
            {
                timing = DateTime.UtcNow;
                logger.LogInformation("Presence in master bathroom shower was detected {Sensors} {ActualPresence}",
                    sensorPresence.Select(s => new { s.EntityId, s.State }),
                    actualPresence.State
                );
            });

        actualPresence.StateChanges()
            .Where(s => s.New.IsOff() && timing is not null)
            .Subscribe(_ =>
            {
                if (!timing.HasValue)
                {
                    throw new ArgumentNullException(nameof(timing));
                }

                EntityMetrics.ShowerDuration.Record((DateTime.UtcNow - timing.Value).TotalSeconds, room);
                timing = null;
            });
    }
}
