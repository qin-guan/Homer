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
        var waterHeaterMinutesOn =
            EntityMetrics.MeterInstance.CreateCounter<int>("homer.master_bathroom_presence_metrics.shower_duration");

        var sensorPresence = new List<BinarySensorEntity>
        {
            binarySensorEntities.MasterBathroomSinkMotionOccupancy,
            binarySensorEntities.MasterBathroomToiletMotionOccupancy,
        };

        var actualPresence = inputBooleanEntities.MasterBathroomPresence;
        DateTime? timing = null;

        sensorPresence.StateChanges()
            .Where(_ => sensorPresence.All(s => s.IsOff()) && actualPresence.IsOn() && timing is null)
            .Subscribe(_ =>
            {
                timing = DateTime.Now;
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

                waterHeaterMinutesOn.Add((int)(DateTime.Now - timing).Value.TotalMinutes);
                timing = null;
            });
    }
}
