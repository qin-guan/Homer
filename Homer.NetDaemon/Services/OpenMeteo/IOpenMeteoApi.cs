using System.Text.Json.Serialization;
using Refit;

namespace Homer.NetDaemon.Services.OpenMeteo;

public interface IOpenMeteoApi
{
    [Get("/v1/forecast")]
    Task<OpenMeteoResponse> GetForecastAsync([Query] double latitude = 1.3508, [Query] double longitude = 103.8480, [Query] string current = "weather_code");
}

public class OpenMeteoResponse
{
    [JsonPropertyName("current")]
    public CurrentWeather Current { get; set; }
}

public class CurrentWeather
{
    [JsonPropertyName("weather_code")]
    public int WeatherCode { get; set; }
}
