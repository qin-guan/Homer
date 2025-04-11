using System.Reactive.Linq;
using Homer.NetDaemon.Services.DataMall;
using Homer.NetDaemon.Services.DataMall.BusArrival;

namespace Homer.NetDaemon.Services;

public class DataMallObservableFactoryService(IDataMallApi dataMallApi)
{
    public IObservable<BusArrivalResponse> CreateWithBusStopCode(string code)
    {
        return Observable.Interval(TimeSpan.FromSeconds(5))
            .SelectMany(async (i) =>
                await dataMallApi.GetBusArrivalAsync(code)
            );
    }
}