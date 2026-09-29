using System.Diagnostics.Metrics;
using System.Reactive.Linq;
using Homer.NetDaemon.Entities;
using Homer.ServiceDefaults.Metrics;
using NetDaemon.AppModel;
using NetDaemon.HassModel.Entities;

namespace Homer.NetDaemon.Apps.WaterHeater;

/// <summary>
/// Records each water heater run, plus pull-based gauges for whether it is heating and the budget left. Each app
/// instance owns a Meter so a NetDaemon reload doesn't leave stale callbacks behind.
/// </summary>
[NetDaemonApp]
public sealed class WaterHeaterMetrics : IDisposable
{
    private readonly Meter _meter = new(EntityMetrics.MeterName);

    public WaterHeaterMetrics(
        ILogger<WaterHeaterMetrics> logger,
        SwitchEntities switchEntities,
        InputNumberEntities inputNumberEntities)
    {
        var heater = switchEntities.WaterHeaterSwitch;
        var minutesLeft = inputNumberEntities.WaterHeaterMinutesLeft;

        heater.StateChanges()
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

        _meter.CreateObservableGauge("homer.water_heater.on", () => heater.IsOn() ? 1 : 0,
            "1", "1 while the water heater is heating");
        _meter.CreateObservableGauge("homer.water_heater.budget.remaining", () => minutesLeft.State ?? 0,
            "min", "Heating minutes left in today's water heater budget");
    }

    public void Dispose() => _meter.Dispose();
}
