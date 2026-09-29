using System.Reactive.Linq;
using Homer.NetDaemon.Channels;
using Homer.NetDaemon.Services;
using NetDaemon.AppModel;

namespace Homer.NetDaemon.Apps.Balcony;

/// <summary>Closes the balcony blinds without asking when rain is forecast overnight.</summary>
[NetDaemonApp]
public sealed class RainyNightCloseBlinds : IDisposable
{
    private readonly IDisposable _subscription;

    public RainyNightCloseBlinds(ApiObservableFactoryService factory)
    {
        _subscription = factory.CreateRainForecast()
            .Where(_ => TimeOnly.FromDateTime(DateTime.Now).IsBetween(new TimeOnly(21, 0), new TimeOnly(9, 0)))
            .Subscribe(_ => BalconyBlindsChannel.CloseAll());
    }

    public void Dispose() => _subscription.Dispose();
}
