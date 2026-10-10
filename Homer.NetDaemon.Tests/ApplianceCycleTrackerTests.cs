using Homer.NetDaemon.Dashboard;

namespace Homer.NetDaemon.Tests;

public class ApplianceCycleTrackerTests
{
    private static readonly DateTime Start = new(2026, 10, 10, 9, 0, 0);

    /// <summary>Feeds one reading a minute: (minutes, watts) holds that draw for that many minutes.</summary>
    private static DateTime Feed(ApplianceCycleTracker tracker, DateTime from, params (int Minutes, double? Watts)[] steps)
    {
        var time = from;
        foreach (var (minutes, watts) in steps)
        {
            for (var i = 0; i < minutes; i++)
            {
                tracker.Update(time, watts);
                time = time.AddMinutes(1);
            }
        }

        return time;
    }

    [Fact]
    public void A_wash_finishes_once_the_draw_stays_low()
    {
        var tracker = ApplianceCycleTracker.WashingMachine();

        Feed(tracker, Start, (2, 0), (45, 450), (7, 1));

        Assert.Equal(ApplianceStatus.Finished, tracker.Status);
        Assert.Equal(Start.AddMinutes(2), tracker.StartedAt);
        Assert.Equal(Start.AddMinutes(47), tracker.FinishedAt);
    }

    [Fact]
    public void A_soak_in_the_middle_of_a_wash_does_not_finish_it()
    {
        var tracker = ApplianceCycleTracker.WashingMachine();

        Feed(tracker, Start, (20, 450), (4, 2), (20, 450));

        Assert.Equal(ApplianceStatus.Running, tracker.Status);
    }

    [Fact]
    public void Standby_draw_between_the_thresholds_keeps_the_cycle_running()
    {
        var tracker = ApplianceCycleTracker.WashingMachine();

        Feed(tracker, Start, (20, 450), (15, 8));

        Assert.Equal(ApplianceStatus.Running, tracker.Status);
    }

    [Fact]
    public void A_short_blip_is_not_a_cycle()
    {
        var tracker = ApplianceCycleTracker.WashingMachine();

        Feed(tracker, Start, (3, 300), (6, 0));

        Assert.Equal(ApplianceStatus.Idle, tracker.Status);
        Assert.Null(tracker.FinishedAt);
    }

    [Fact]
    public void An_unavailable_plug_leaves_the_cycle_alone()
    {
        var tracker = ApplianceCycleTracker.Dishwasher();

        var time = Feed(tracker, Start, (30, 1800));
        Feed(tracker, time, (60, null));

        Assert.Equal(ApplianceStatus.Running, tracker.Status);
    }

    [Fact]
    public void A_new_cycle_clears_the_previous_finish()
    {
        var tracker = ApplianceCycleTracker.WashingMachine();

        var time = Feed(tracker, Start, (30, 450), (6, 0));
        Assert.Equal(ApplianceStatus.Finished, tracker.Status);

        Feed(tracker, time, (5, 450));

        Assert.Equal(ApplianceStatus.Running, tracker.Status);
        Assert.Null(tracker.FinishedAt);
    }
}
