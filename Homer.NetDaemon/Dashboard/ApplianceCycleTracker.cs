namespace Homer.NetDaemon.Dashboard;

/// <summary>
/// Follows an appliance's wash cycle from its smart plug's power draw. A cycle starts once the draw stays above
/// <c>runningWatts</c> for <c>startAfter</c>, and finishes once it stays below <c>idleWatts</c> for
/// <c>finishAfter</c>, which has to outlast soaking and drying pauses. Very short "cycles" are dropped.
/// </summary>
public sealed class ApplianceCycleTracker(
    double runningWatts,
    double idleWatts,
    TimeSpan startAfter,
    TimeSpan finishAfter,
    TimeSpan minimumCycle)
{
    private DateTime? _aboveSince;
    private DateTime? _belowSince;

    public ApplianceStatus Status { get; private set; } = ApplianceStatus.Idle;

    public DateTime? StartedAt { get; private set; }

    public DateTime? FinishedAt { get; private set; }

    public static ApplianceCycleTracker WashingMachine() =>
        new(15, 4, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10));

    public static ApplianceCycleTracker Dishwasher() =>
        new(15, 4, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(20));

    /// <param name="watts">Current draw, or null while the plug is unavailable (the cycle is left as it was).</param>
    public void Update(DateTime now, double? watts)
    {
        if (watts is not { } draw)
        {
            return;
        }

        if (draw >= runningWatts)
        {
            _belowSince = null;
            _aboveSince ??= now;

            if (Status != ApplianceStatus.Running && now - _aboveSince >= startAfter)
            {
                Status = ApplianceStatus.Running;
                StartedAt = _aboveSince;
                FinishedAt = null;
            }

            return;
        }

        _aboveSince = null;

        if (draw >= idleWatts)
        {
            // Standby-ish draw between the thresholds, as in a soak: neither starting nor finishing.
            _belowSince = null;
            return;
        }

        _belowSince ??= now;
        if (Status != ApplianceStatus.Running || now - _belowSince < finishAfter)
        {
            return;
        }

        if (StartedAt is { } started && _belowSince - started >= minimumCycle)
        {
            Status = ApplianceStatus.Finished;
            FinishedAt = _belowSince;
        }
        else
        {
            Status = ApplianceStatus.Idle;
            StartedAt = null;
        }
    }
}
