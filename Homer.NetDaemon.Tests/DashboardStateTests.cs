using Homer.NetDaemon.Dashboard;

namespace Homer.NetDaemon.Tests;

public class DashboardStateTests
{
    [Fact]
    public void Republishing_an_identical_snapshot_does_not_raise_changed()
    {
        var state = new DashboardState();
        var changes = 0;
        state.Changed += () => changes++;

        state.Publish(DemoScenarios.Create("evening"));
        var first = state.Current;
        state.Publish(DemoScenarios.Create("evening") with { WaterHeater = first!.Home.WaterHeater });

        Assert.Equal(1, changes);
    }

    [Fact]
    public void Dismissing_a_card_hides_it_and_raises_changed()
    {
        var state = new DashboardState();
        state.Publish(DemoScenarios.Create("rain"));
        var laundry = state.Current!.Pages.SelectMany(p => p.Cards).Single(c => c.Kind == CardKind.Laundry);
        var changes = 0;
        state.Changed += () => changes++;

        state.Dismiss(laundry.Key, laundry.DismissFor!.Value);

        Assert.Equal(1, changes);
        Assert.DoesNotContain(state.Current!.Pages.SelectMany(p => p.Cards), c => c.Kind == CardKind.Laundry);
    }

    [Fact]
    public void Viewers_are_counted_until_their_dashboards_close()
    {
        var state = new DashboardState();
        var watching = new List<bool>();
        using var subscription = state.HasViewers.Subscribe(watching.Add);

        var hub = state.AddViewer();
        var phone = state.AddViewer();
        hub.Dispose();
        phone.Dispose();
        phone.Dispose();

        Assert.Equal([false, true, false], watching);
    }

    [Fact]
    public void Snapshots_compare_by_value()
    {
        Assert.Equal(DemoScenarios.Create("away") with { WaterHeater = new WaterHeaterInfo() },
            DemoScenarios.Create("away") with { WaterHeater = new WaterHeaterInfo() });
    }
}
