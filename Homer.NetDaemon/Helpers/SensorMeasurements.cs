using System.Diagnostics.Metrics;
using Homer.NetDaemon.Entities;

namespace Homer.NetDaemon.Helpers;

/// <summary>Turns NetDaemon's cached sensor states into measurements for pull-based (observable) instruments.</summary>
public static class SensorMeasurements
{
    public static Measurement<double> Measure(string entityId, double value) =>
        new(value, new KeyValuePair<string, object?>("entity_id", entityId));

    public static IEnumerable<Measurement<double>> Readings(IEnumerable<NumericSensorEntity> sensors, bool toWatts = false)
    {
        foreach (var s in sensors)
        {
            if (Read(s, toWatts) is { } value)
            {
                yield return Measure(s.EntityId, value);
            }
        }
    }

    // Unavailable / unknown sensors yield no data point instead of a misleading 0.
    public static double? Read(NumericSensorEntity sensor, bool toWatts = false)
    {
        if (sensor.State is not { } value || double.IsNaN(value))
        {
            return null;
        }

        return toWatts && sensor.Attributes?.UnitOfMeasurement == "kW" ? value * 1000 : value;
    }
}
