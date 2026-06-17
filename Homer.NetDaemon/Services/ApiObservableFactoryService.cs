using System.Reactive.Linq;
using Homer.NetDaemon.Services.DataMall;
using Homer.NetDaemon.Services.DataMall.BusArrival;
using Homer.NetDaemon.Services.OpenMeteo;

namespace Homer.NetDaemon.Services;

public class ApiObservableFactoryService(IDataMallApi dataMallApi, IOpenMeteoApi openMeteoApi)
{
    public IObservable<BusArrivalResponse> CreateWithBusStopCode(string code)
    {
        return Observable.Interval(TimeSpan.FromSeconds(5)).SelectMany(async (i) =>
            await dataMallApi.GetBusArrivalAsync(code)
        );
    }

    public IObservable<OpenMeteoResponse> CreateForecast()
    {
        return Observable.Timer(TimeSpan.Zero, TimeSpan.FromMinutes(5))
            .SelectMany(async (i) => await openMeteoApi.GetForecastAsync());
    }
}