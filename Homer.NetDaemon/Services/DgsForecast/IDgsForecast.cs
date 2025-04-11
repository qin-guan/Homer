using Homer.NetDaemon.Services.DgsForecast.Forecast;
using WebApiClientCore.Attributes;

namespace Homer.NetDaemon.Services.DgsForecast;

public interface IDgsForecast
{
    [HttpGet("/v2/real-time/api/two-hr-forecast")]
    public Task<ForecastResponse> GetForecastAsync();
}