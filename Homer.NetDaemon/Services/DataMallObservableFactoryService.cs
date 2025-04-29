using System.Reactive.Linq;
using Homer.NetDaemon.Services.DataMall;
using Homer.NetDaemon.Services.DataMall.BusArrival;
using Homer.NetDaemon.Services.DgsForecast;
using Homer.NetDaemon.Services.DgsForecast.Forecast;

namespace Homer.NetDaemon.Services;

public class DataMallObservableFactoryService(IDataMallApi dataMallApi, IDgsForecast dgsForecast)
{
    public IObservable<BusArrivalResponse> CreateWithBusStopCode(string code)
    {
        return Observable.Interval(TimeSpan.FromSeconds(5)).SelectMany(async (i) =>
            await dataMallApi.GetBusArrivalAsync(code)
        );
    }

    public IObservable<ForecastResponse> CreateForecast()
    {
        return Observable.Interval(TimeSpan.FromSeconds(5))
            .SelectMany(async (i) => await dgsForecast.GetForecastAsync());
    }
}