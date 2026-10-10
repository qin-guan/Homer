using Homer.NetDaemon.Services.OpenMeteo;

namespace Homer.NetDaemon.Dashboard;

/// <summary>Dashboard wording and icons for WMO weather codes, as reported by Open-Meteo.</summary>
public static class WeatherText
{
    public static bool IsRainy(int code) => OpenMeteoWmoMapper.IsRainy(code);

    public static string Describe(int code) => code switch
    {
        0 => "晴",
        1 => "晴间多云",
        2 => "多云",
        3 => "阴",
        45 or 48 => "雾",
        51 or 53 or 55 => "毛毛雨",
        56 or 57 => "冻毛毛雨",
        61 => "小雨",
        63 => "中雨",
        65 => "大雨",
        66 or 67 => "冻雨",
        71 or 73 or 75 or 77 => "雪",
        80 => "阵雨",
        81 => "中阵雨",
        82 => "强阵雨",
        85 or 86 => "阵雪",
        95 => "雷阵雨",
        96 or 99 => "雷暴",
        _ => "—"
    };

    /// <summary>Font Awesome icon plus a colour class (wx-sun, wx-cloud, wx-rain, wx-bolt, wx-moon).</summary>
    public static (string Icon, string Tone) Icon(int code, bool isDay) => code switch
    {
        0 => isDay ? ("fa-sun", "wx-sun") : ("fa-moon", "wx-moon"),
        1 or 2 => isDay ? ("fa-cloud-sun", "wx-sun") : ("fa-cloud-moon", "wx-moon"),
        3 => ("fa-cloud", "wx-cloud"),
        45 or 48 => ("fa-smog", "wx-cloud"),
        65 or 82 => ("fa-cloud-showers-heavy", "wx-rain"),
        80 or 81 => isDay ? ("fa-cloud-sun-rain", "wx-rain") : ("fa-cloud-moon-rain", "wx-rain"),
        >= 51 and <= 67 => ("fa-cloud-rain", "wx-rain"),
        >= 71 and <= 77 or 85 or 86 => ("fa-snowflake", "wx-cloud"),
        >= 95 => ("fa-cloud-bolt", "wx-bolt"),
        _ => ("fa-cloud", "wx-cloud")
    };

    /// <summary>Maps a Home Assistant weather condition to the closest WMO code, for when Open-Meteo is unreachable.</summary>
    public static int FromHomeAssistantCondition(string? condition) => condition switch
    {
        "sunny" or "clear-night" => 0,
        "partlycloudy" => 2,
        "cloudy" or "windy" or "windy-variant" or "exceptional" => 3,
        "fog" => 45,
        "rainy" => 61,
        "pouring" => 65,
        "snowy" or "snowy-rainy" or "hail" => 71,
        "lightning" or "lightning-rainy" => 95,
        _ => 3
    };
}
