using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Homer.NetDaemon.Entities;
using Homer.ServiceDefaults.Metrics;
using NetDaemon.AppModel;
using NetDaemon.HassModel.Entities;

namespace Homer.NetDaemon.Apps.Bathroom;

[NetDaemonApp]
public class WaterHeaterMetrics
{
    public WaterHeaterMetrics(ILogger<WaterHeaterMetrics> logger, SwitchEntities switchEntities, IScheduler scheduler)
    {
        switchEntities.WaterHeaterSwitch.StateChanges()
            .Where(s => s.Old.IsOn() && s.New.IsOff())
            .Subscribe(s =>
            {
                var duration = s.New?.LastChanged - s.Old?.LastChanged;
                if (duration is null || duration.Value <= TimeSpan.Zero)
                {
                    return;
                }

                // Seconds, not truncated whole minutes: short cycles used to be recorded as 0.
                EntityMetrics.WaterHeaterRunDuration.Record(duration.Value.TotalSeconds);

                logger.LogInformation("Recorded water heater run of {Minutes:F1} minutes",
                    duration.Value.TotalMinutes);
            });
    }
}
