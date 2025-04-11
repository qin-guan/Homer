using System.Text.Json.Serialization;

namespace Homer.NetDaemon.Services.DgsForecast.Forecast;

public class ForecastResponse
{
    [JsonPropertyName("data")] public Data Data { get; set; }
}

public class Data
{
    [JsonPropertyName("items")] public List<Item> Items { get; set; }
}

public class Item
{
    [JsonPropertyName("forecasts")] public List<Forecast> Forecasts { get; set; }
}

public class Forecast
{
    [JsonPropertyName("area")] public string Area { get; set; }
    [JsonPropertyName("forecast")] public string Value { get; set; }
}