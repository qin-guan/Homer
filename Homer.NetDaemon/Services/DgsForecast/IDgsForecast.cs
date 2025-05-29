using Homer.NetDaemon.Services.DgsForecast.Forecast;
using Refit;

namespace Homer.NetDaemon.Services.DgsForecast;

public interface IDgsForecast
{
    [Get("/v2/real-time/api/two-hr-forecast")]
    public Task<ForecastResponse> GetForecastAsync();
}