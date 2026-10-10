using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Homer.NetDaemon.Dashboard;

public sealed record DashboardView(HomeSnapshot Home, EquatableList<CardPage> Pages);

/// <summary>
/// The dashboard's current view, published by the SmartDashboard app and read by every open dashboard. Also holds
/// dismissed cards, so dismissing one on the living room screen hides it everywhere.
/// </summary>
public sealed class DashboardState
{
    private readonly object _gate = new();
    private readonly object _viewersGate = new();
    private readonly Dictionary<string, DateTime> _dismissedUntilUtc = [];
    private readonly BehaviorSubject<int> _viewers = new(0);
    private HomeSnapshot? _home;
    private DashboardView? _view;

    /// <summary>The latest view, or null until Home Assistant has been read once.</summary>
    public DashboardView? Current => Volatile.Read(ref _view);

    /// <summary>Raised when the view changes. Handlers run outside of this service's lock.</summary>
    public event Action? Changed;

    /// <summary>Whether any dashboard is open, so feeds that poll external APIs only run while someone can see them.</summary>
    public IObservable<bool> HasViewers => _viewers.Select(v => v > 0).DistinctUntilChanged();

    public IDisposable AddViewer()
    {
        // A separate lock from _gate: viewer subscribers may publish synchronously, which takes _gate.
        lock (_viewersGate)
        {
            _viewers.OnNext(_viewers.Value + 1);
        }

        return Disposable.Create(() =>
        {
            lock (_viewersGate)
            {
                _viewers.OnNext(Math.Max(0, _viewers.Value - 1));
            }
        });
    }

    public void Publish(HomeSnapshot home)
    {
        DashboardView? changed;
        lock (_gate)
        {
            if (home == _home)
            {
                return;
            }

            _home = home;
            changed = RebuildCore();
        }

        if (changed is not null)
        {
            Changed?.Invoke();
        }
    }

    public void Dismiss(string key, TimeSpan duration)
    {
        DashboardView? changed;
        lock (_gate)
        {
            _dismissedUntilUtc[key] = DateTime.UtcNow + duration;
            changed = RebuildCore();
        }

        if (changed is not null)
        {
            Changed?.Invoke();
        }
    }

    private DashboardView? RebuildCore()
    {
        if (_home is null)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        foreach (var expired in _dismissedUntilUtc.Where(d => d.Value <= now).Select(d => d.Key).ToList())
        {
            _dismissedUntilUtc.Remove(expired);
        }

        var view = new DashboardView(_home, SmartStack.Build(_home, _dismissedUntilUtc.Keys.ToHashSet()));
        if (view == _view)
        {
            return null;
        }

        Volatile.Write(ref _view, view);
        return view;
    }
}
