using Homer.NetDaemon.Services.DataMall.BusArrival;
using Refit;

namespace Homer.NetDaemon.Services.DataMall;

public interface IDataMallApi
{
    [Get("/ltaodataservice/v3/BusArrival")]
    public Task<BusArrivalResponse> GetBusArrivalAsync([Query] [AliasAs("BusStopCode")] string busStopCode);
}