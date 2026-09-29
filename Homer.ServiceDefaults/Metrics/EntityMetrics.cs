using System.Diagnostics.Metrics;

namespace Homer.ServiceDefaults.Metrics;

/// <summary>
/// Shared instruments for home-level telemetry. Instruments are created once and tagged by room / automation,
/// rather than one bespoke counter per app, so dashboards can group and compare them.
/// </summary>
public static class EntityMetrics
{
    public const string MeterName = "Homer";

    public static readonly Meter MeterInstance = new(MeterName);

    /// <summary>Events an automation evaluated (its input activity), tagged by automation name.</summary>
    public static readonly Counter<long> AutomationEvents = MeterInstance.CreateCounter<long>(
        "homer.automation.events", "{event}", "Events evaluated by a Homer automation");

    /// <summary>Home Assistant bus traffic by event type and (for state changes) entity domain. Low cardinality on purpose.</summary>
    public static readonly Counter<long> HomeAssistantEvents = MeterInstance.CreateCounter<long>(
        "homer.ha.events", "{event}", "Events received from Home Assistant");

    /// <summary>Length of each shower, tagged by room.</summary>
    public static readonly Histogram<double> ShowerDuration = MeterInstance.CreateHistogram<double>(
        "homer.shower.duration", "s", "Duration of detected showers");

    /// <summary>Length of each water heater run. Count = heater cycles, sum = total heating time.</summary>
    public static readonly Histogram<double> WaterHeaterRunDuration = MeterInstance.CreateHistogram<double>(
        "homer.water_heater.run.duration", "s", "Duration of each water heater run");

    /// <summary>Duration bucket boundaries (seconds) that suit showers and heater runs; default OTel buckets are too coarse.</summary>
    public static readonly double[] DurationBucketsSeconds = [60, 120, 180, 300, 420, 600, 900, 1200, 1800];

    public static void AutomationEvent(string automation) =>
        AutomationEvents.Add(1, new KeyValuePair<string, object?>("automation", automation));
}
