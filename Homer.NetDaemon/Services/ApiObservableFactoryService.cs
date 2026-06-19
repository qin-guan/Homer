using System.Collections.Concurrent;
using System.Reactive.Linq;
using Homer.NetDaemon.Services.DataMall;
using Homer.NetDaemon.Services.DataMall.BusArrival;
using Homer.NetDaemon.Services.OpenMeteo;

namespace Homer.NetDaemon.Services;

public class ApiObservableFactoryService(IDataMallApi dataMallApi, IOpenMeteoApi openMeteoApi)
{
    private readonly ConcurrentDictionary<string, IObservable<BusArrivalResponse>> _busStopCache = new();
    private IObservable<OpenMeteoResponse>? _forecastCache;

    public IObservable<BusArrivalResponse> CreateWithBusStopCode(string code)
    {
        return _busStopCache.GetOrAdd(code, c => Observable.Interval(TimeSpan.FromSeconds(5))
            .SelectMany(async i => await dataMallApi.GetBusArrivalAsync(c))
            .Replay(1)
            .RefCount());
    }

    public IObservable<OpenMeteoResponse> CreateForecast()
    {
        return _forecastCache ??= Observable.Timer(TimeSpan.Zero, TimeSpan.FromMinutes(5))
            .SelectMany(async i => await openMeteoApi.GetForecastAsync())
            .Replay(1)
            .RefCount();
    }
}