using System.Collections.Concurrent;
using System.Reactive.Linq;
using Homer.NetDaemon.Services.DataMall;
using Homer.NetDaemon.Services.DataMall.BusArrival;
using Homer.NetDaemon.Services.OpenMeteo;

namespace Homer.NetDaemon.Services;

public class ApiObservableFactoryService(
    IDataMallApi dataMallApi,
    IOpenMeteoApi openMeteoApi,
    ILogger<ApiObservableFactoryService> logger)
{
    private readonly ConcurrentDictionary<string, IObservable<BusArrivalResponse>> _busStopCache = new();
    private IObservable<OpenMeteoResponse>? _forecastCache;

    public IObservable<BusArrivalResponse> CreateWithBusStopCode(string code)
    {
        return _busStopCache.GetOrAdd(code, c => Observable.Interval(TimeSpan.FromSeconds(5))
            .SelectMany(_ => FetchOrSkip(() => dataMallApi.GetBusArrivalAsync(c), $"bus arrivals for stop {c}"))
            .Replay(1)
            .RefCount());
    }

    public IObservable<OpenMeteoResponse> CreateForecast()
    {
        return _forecastCache ??= Observable.Timer(TimeSpan.Zero, TimeSpan.FromMinutes(5))
            .SelectMany(_ => FetchOrSkip(() => openMeteoApi.GetForecastAsync(), "the weather forecast"))
            .Replay(1)
            .RefCount();
    }

    /// <summary>Descriptions of rainy current weather, emitted whenever the kind of rain changes.</summary>
    public IObservable<string> CreateRainForecast()
    {
        return CreateForecast()
            .Select(f => f.Current.WeatherCode)
            .Where(v => OpenMeteoWmoMapper.IsRainy(v))
            .Select(v => OpenMeteoWmoMapper.GetWeatherDescription(v))
            .DistinctUntilChanged();
    }

    // These feeds are shared and replayed: a single failed request would otherwise error the stream and leave every
    // subscriber, now and later, without data until Homer restarts. Skip the failed poll and try again next time.
    private IObservable<T> FetchOrSkip<T>(Func<Task<T>> fetch, string what) =>
        Observable.FromAsync(fetch)
            .Catch<T, Exception>(e =>
            {
                logger.LogWarning(e, "Failed to fetch {What}; retrying on the next poll", what);
                return Observable.Empty<T>();
            });
}
