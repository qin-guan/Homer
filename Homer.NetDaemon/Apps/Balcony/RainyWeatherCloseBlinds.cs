using Homer.NetDaemon.Entities;
using Homer.NetDaemon.Services;
using NetDaemon.AppModel;

namespace Homer.NetDaemon.Apps.Balcony;

[NetDaemonApp]
public class RainyWeatherCloseBlinds(
    DataMallObservableFactoryService factory,
    RemoteEntities remote,
    ILogger<RainyWeatherCloseBlinds> logger)
    : IAsyncInitializable, IAsyncDisposable
{
    private IDisposable? _disposable;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        _disposable = factory.CreateForecast().Subscribe(forecast =>
        {
            var bishan = forecast.Data.Items.First().Forecasts.First(f => f.Area == "Bishan");
            if (bishan.Value is not (
                "Moderate Rain" or
                "Heavy Rain" or
                "Passing Showers" or
                "Light Showers" or
                "Showers" or
                "Heavy Showers" or
                "Thundery Showers" or
                "Heavy Thundery Showers" or
                "Heavy Thundery Showers with Gusty Winds"
                )) return;
            
            remote.LivingRoomRemote.SendCommand("All Down", "Balcony Blinds");
            logger.LogInformation("Closing blinds due to poor weather of {Weather}", bishan.Value);
        });
    }

    public ValueTask DisposeAsync()
    {
        _disposable?.Dispose();
        return ValueTask.CompletedTask;
    }
}