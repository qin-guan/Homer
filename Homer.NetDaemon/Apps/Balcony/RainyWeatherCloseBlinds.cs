using System.Reactive.Linq;
using Homer.NetDaemon.Entities;
using Homer.NetDaemon.Services;
using NetDaemon.AppModel;

namespace Homer.NetDaemon.Apps.Balcony;

[NetDaemonApp]
public class RainyWeatherCloseBlinds(
    DataMallObservableFactoryService factory,
    RemoteEntities remote,
    NotifyServices notify,
    ILogger<RainyWeatherCloseBlinds> logger
)
    : IAsyncInitializable, IAsyncDisposable
{
    private IDisposable? _disposable;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        _disposable = factory.CreateForecast()
            .Select(f => f.Data.Items.First().Forecasts.First(f => f.Area == "Bishan").Value)
            .Where(v => v is
                "Moderate Rain" or
                "Heavy Rain" or
                "Passing Showers" or
                "Light Showers" or
                "Showers" or
                "Heavy Showers" or
                "Thundery Showers" or
                "Heavy Thundery Showers" or
                "Heavy Thundery Showers with Gusty Winds"
            )
            .DistinctUntilChanged()
            .Subscribe(forecast =>
            {
                remote.LivingRoomRemote.SendCommand("All Down", "Balcony Blinds");
                logger.LogInformation("Closing blinds due to poor weather of {Weather}", forecast);
                notify.MobileAppQinsIphone($"Closing blinds due to poor weather of {forecast}");
            });
    }

    public ValueTask DisposeAsync()
    {
        _disposable?.Dispose();
        return ValueTask.CompletedTask;
    }
}