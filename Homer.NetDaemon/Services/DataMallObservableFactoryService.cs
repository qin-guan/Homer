using Homer.NetDaemon.Services.DataMall;
using Homer.NetDaemon.Services.DataMall.BusArrival;
using R3;

namespace Homer.NetDaemon.Services;

public class DataMallObservableFactoryService(IDataMallApi dataMallApi)
{
    // public Observable<BusArrivalResponse> CreateWithBusStopCode(string code)
    // {
    //     return Observable.Interval(TimeSpan.FromSeconds(1))
    //         .Select((_, i) => i)
    //         .Do((tick) => Console.WriteLine($"[BusArrivalPoller] === Interval Tick #{tick} received ===")) // DEBUG 1: Log each tick from Interval
    //         .SelectAwait(async (_, ct) => // Use async/await directly
    //         {
    //                 return await dataMallApi.GetBusArrivalAsync(code);
    //         }, AwaitOperation.Drop);
    // }
}