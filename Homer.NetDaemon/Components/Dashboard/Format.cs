using System.Globalization;
using Homer.NetDaemon.Dashboard;

namespace Homer.NetDaemon.Components.Dashboard;

/// <summary>Chinese wording for times, durations and readings on the dashboard.</summary>
public static class Format
{
    private static readonly string[] Weekdays = ["星期日", "星期一", "星期二", "星期三", "星期四", "星期五", "星期六"];

    public static string Date(DateTime date) => $"{date.Month}月{date.Day}日 {Weekdays[(int)date.DayOfWeek]}";

    public static string Greeting(DateTime now) => now.Hour switch
    {
        < 5 => "夜深了",
        < 11 => "早上好",
        < 13 => "中午好",
        < 18 => "下午好",
        < 23 => "晚上好",
        _ => "夜深了"
    };

    public static string Ago(DateTime then, DateTime now)
    {
        var minutes = (int)(now - then).TotalMinutes;
        return minutes switch
        {
            < 1 => "刚刚",
            < 60 => $"{minutes} 分钟前",
            _ => $"{minutes / 60} 小时前"
        };
    }

    public static string Duration(TimeSpan duration)
    {
        var minutes = (int)Math.Max(0, duration.TotalMinutes);
        return minutes switch
        {
            < 1 => "不到 1 分钟",
            < 60 => $"{minutes} 分钟",
            _ when minutes % 60 == 0 => $"{minutes / 60} 小时",
            _ => $"{minutes / 60} 小时 {minutes % 60} 分"
        };
    }

    /// <summary>Whole minutes until <paramref name="at"/>, or null when it's due now.</summary>
    public static int? MinutesUntil(DateTime at, DateTime now)
    {
        var minutes = (int)Math.Floor((at - now).TotalMinutes);
        return minutes <= 0 ? null : minutes;
    }

    public static string Power(double watts) =>
        watts >= 1000 ? $"{watts / 1000:0.00} kW" : $"{watts:0} W";

    public static string Temperature(double? temperature) => temperature is { } t ? $"{t:0.#}°" : "--°";

    public static string Invariant(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    public static string IsoUtc(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToString("o", CultureInfo.InvariantCulture);

    public static string AirconMode(string? mode) => mode switch
    {
        "cool" => "制冷",
        "dry" => "除湿",
        "fan_only" => "送风",
        "heat" => "制热",
        "auto" or "heat_cool" => "自动",
        _ => "运行中"
    };

    public static string DeviceIcon(DeviceKind kind) => kind switch
    {
        DeviceKind.Aircon => "fa-snowflake",
        DeviceKind.Fan => "fa-fan",
        _ => "fa-lightbulb"
    };

    /// <summary>A system colour per part of the electricity breakdown, the same on the card and its sheet.</summary>
    public static string EnergyColor(string part) => part switch
    {
        EnergyPart.Aircons => "var(--cyan)",
        "热水器" => "var(--orange)",
        "洗衣机" => "var(--indigo)",
        "洗碗机" => "var(--teal)",
        _ => "var(--yellow)"
    };

    public static string EnergyIcon(string part) => part switch
    {
        EnergyPart.Aircons => "fa-snowflake",
        "热水器" => "fa-fire-flame-curved",
        "洗衣机" => "fa-shirt",
        "洗碗机" => "fa-utensils",
        _ => "fa-plug"
    };

    /// <summary>A share of the total draw as a whole percentage, without claiming 0% for something that draws power.</summary>
    public static string Share(double watts, double total)
    {
        var percent = total <= 0 ? 0 : watts / total * 100;
        return percent is > 0 and < 1 ? "<1%" : $"{Math.Round(percent):0}%";
    }

    public static string BlindsState(double fractionClosed)
    {
        var percent = (int)Math.Round(fractionClosed * 100);
        return percent <= 2 ? "全开" : percent >= 98 ? "全关" : $"关 {percent}%";
    }

    private static readonly string[] PersonColors =
        ["var(--blue)", "var(--orange)", "var(--green)", "var(--purple)", "var(--teal)", "var(--pink)", "var(--indigo)"];

    /// <summary>A system colour per person that stays the same across restarts, for their monogram.</summary>
    public static string PersonColor(string id) =>
        PersonColors[id.Aggregate(17, (hash, c) => (hash * 31 + c) % 7919) % PersonColors.Length];

    public static string SizeClass(CardSize size) => size switch
    {
        CardSize.Large => "sz-l",
        CardSize.Medium => "sz-m",
        _ => "sz-s"
    };
}
