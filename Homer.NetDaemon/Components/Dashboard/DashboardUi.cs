using Homer.NetDaemon.Dashboard;

namespace Homer.NetDaemon.Components.Dashboard;

public enum DashboardSheet
{
    None,
    Blinds,
    WaterHeater
}

/// <summary>Cascaded to every dashboard component: runs actions (or ignores them in demo mode) and opens sheets.</summary>
public sealed class DashboardUi(
    DashboardActions actions,
    ILogger logger,
    bool demo,
    Func<DateTime> now,
    Action<DashboardSheet> openSheet,
    Action interacted)
{
    public bool Demo { get; } = demo;

    /// <summary>The time relative readings ("in 3 minutes") are measured from: the scenario's in demo mode.</summary>
    public DateTime Now => now();

    public void Run(Action<DashboardActions> action)
    {
        interacted();
        if (Demo)
        {
            return;
        }

        try
        {
            action(actions);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "A dashboard action failed");
        }
    }

    public void OpenSheet(DashboardSheet sheet) => openSheet(sheet);
}
