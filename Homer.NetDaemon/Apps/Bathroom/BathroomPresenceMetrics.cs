using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Homer.NetDaemon.Entities;
using Homer.ServiceDefaults.Metrics;
using NetDaemon.AppModel;
using NetDaemon.HassModel.Entities;

namespace Homer.NetDaemon.Apps.Bathroom;

[NetDaemonApp]
public class BathroomPresenceMetrics
{
    public BathroomPresenceMetrics(
        ILogger<BathroomPresenceMetrics> logger,
        InputBooleanEntities inputBooleanEntities,
        BinarySensorEntities binarySensorEntities,
        IScheduler scheduler
    )
    {
        var sensorPresence = new List<BinarySensorEntity>
        {
            binarySensorEntities.BathroomMotionOccupancy
        };

        var actualPresence = inputBooleanEntities.BathroomPresence;
        DateTime? timing = null;
        var room = new KeyValuePair<string, object?>("room", "bathroom");

        sensorPresence.StateChanges()
            .Where(_ => sensorPresence.All(s => s.IsOff()) && actualPresence.IsOn() && timing is null)
            .Subscribe(_ =>
            {
                timing = DateTime.UtcNow;
                logger.LogInformation("Presence in shower was detected {Sensors} {ActualPresence}",
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