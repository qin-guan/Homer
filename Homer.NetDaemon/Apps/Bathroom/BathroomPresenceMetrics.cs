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
        var waterHeaterMinutesOn =
            EntityMetrics.MeterInstance.CreateCounter<int>("homer.bathroom_presence_metrics.shower_duration");

        var sensorPresence = new List<BinarySensorEntity>
        {
            binarySensorEntities.BathroomSinkMotionOccupancy,
            binarySensorEntities.BathroomDoorMotionOccupancy,
            binarySensorEntities.BathroomTuyaPresencePresence
        };

        var actualPresence = inputBooleanEntities.BathroomPresence;
        DateTime? timing = null;

        sensorPresence.StateChanges()
            .Where(_ => sensorPresence.All(s => s.IsOff()) && actualPresence.IsOn())
            .Subscribe(_ =>
            {
                timing = DateTime.Now;
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

                waterHeaterMinutesOn.Add((int)(DateTime.Now - timing).Value.TotalMinutes);
            });
    }
}