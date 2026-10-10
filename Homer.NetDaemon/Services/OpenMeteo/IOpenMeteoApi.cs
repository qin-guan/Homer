using System.Text.Json.Serialization;
using Refit;

namespace Homer.NetDaemon.Services.OpenMeteo;

public interface IOpenMeteoApi
{
    [Get("/v1/forecast")]
    Task<OpenMeteoResponse> GetForecastAsync(
        [Query] double latitude = 1.3508,
        [Query] double longitude = 103.8480,
        [Query] string current = "weather_code,temperature_2m,apparent_temperature,relative_humidity_2m,is_day",
        [Query] string hourly = "temperature_2m,precipitation_probability,weather_code",
        [Query] [AliasAs("forecast_hours")] int forecastHours = 8,
        [Query] string timezone = "auto");
}

public class OpenMeteoResponse
{
    [JsonPropertyName("current")]
    public CurrentWeather Current { get; set; }

    [JsonPropertyName("hourly")]
    public HourlyWeather? Hourly { get; set; }

    /// <summary>Offset of the forecast's local times (timezone=auto) from UTC.</summary>
    [JsonPropertyName("utc_offset_seconds")]
    public int UtcOffsetSeconds { get; set; }
}

public class CurrentWeather
{
    [JsonPropertyName("weather_code")]
    public int WeatherCode { get; set; }

    [JsonPropertyName("temperature_2m")]
    public double? Temperature { get; set; }

    [JsonPropertyName("apparent_temperature")]
    public double? ApparentTemperature { get; set; }

    [JsonPropertyName("relative_humidity_2m")]
    public double? RelativeHumidity { get; set; }

    [JsonPropertyName("is_day")]
    public int? IsDay { get; set; }
}

/// <summary>Hourly forecast as parallel arrays; times are local to the location (timezone=auto) without an offset.</summary>
public class HourlyWeather
{
    [JsonPropertyName("time")]
    public List<string> Time { get; set; } = [];

    [JsonPropertyName("temperature_2m")]
    public List<double?> Temperature { get; set; } = [];

    [JsonPropertyName("precipitation_probability")]
    public List<int?> PrecipitationProbability { get; set; } = [];

    [JsonPropertyName("weather_code")]
    public List<int?> WeatherCode { get; set; } = [];
}
