using System.Diagnostics.Metrics;

namespace Homer.ServiceDefaults.Metrics;

public class EntityMetrics
{
    public static readonly Meter MeterInstance = new(nameof(EntityMetrics));
}