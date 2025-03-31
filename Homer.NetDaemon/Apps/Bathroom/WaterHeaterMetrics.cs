using System.Diagnostics.Metrics;
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
        var waterHeaterMinutesOn =
            EntityMetrics.MeterInstance.CreateCounter<int>("homer.water_heater_metrics.minutes_on");

        switchEntities.WaterHeaterSwitch.StateChanges()
            .Where(s => s.Entity.IsOff())
            .Subscribe((s) =>
            {
                var timeSpan = s.New?.LastChanged - s.Old?.LastChanged;
                if (timeSpan is null)
                {
                    return;
                }

                waterHeaterMinutesOn.Add((int)timeSpan.Value.TotalMinutes);

                logger.LogInformation("Recorded water heater turn on duration of {Minutes}",
                    timeSpan.Value.TotalMinutes);
            });
    }
}