using Homer.NetDaemon.Services.DataMall.BusArrival;
using WebApiClientCore;
using WebApiClientCore.Attributes;

namespace Homer.NetDaemon.Services.DataMall;

public interface IDataMallApi
{
    [HttpGet("/ltaodataservice/v3/BusArrival")]
    public Task<BusArrivalResponse> GetBusArrivalAsync([PathQuery] [AliasAs("BusStopCode")] string busStopCode);
}