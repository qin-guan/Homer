using Homer.NetDaemon.Services;

namespace Homer.NetDaemon.Dashboard;

public enum CardKind
{
    Rain,
    Bus,
    Bathroom,
    WaterHeater,
    Laundry,
    Dishwasher,
    LeftOn,
    Climate,
    ClimateSuggestion,
    FrontDoor,
    Energy,
    Bedtime,
    NowPlaying,
    Maintenance,
    Dehumidifier,
    Arrival,
    Forecast
}

/// <summary>Grid cells a card covers on a 4 × 2 page: small 1 × 1, medium 2 × 1, large 2 × 2.</summary>
public enum CardSize
{
    Small = 1,
    Medium = 2,
    Large = 4
}

/// <param name="Key">Identifies this occurrence of the card, so dismissing it does not hide the next one.</param>
/// <param name="Score">Relevance from 0 to 100; cards below <see cref="SmartStack.MinimumScore"/> are not shown.</param>
/// <param name="Reason">Why the card is surfaced now, shown as a caption.</param>
/// <param name="Subjects">Entity IDs the card is about, such as the devices a "left on" card offers to turn off.</param>
/// <param name="DismissFor">When set, the card can be dismissed, and stays hidden for this long.</param>
public sealed record SmartCard(
    string Key,
    CardKind Kind,
    int Score,
    CardSize Size,
    string? Reason = null,
    EquatableList<string>? Subjects = null,
    TimeSpan? DismissFor = null);

public sealed record CardPage(EquatableList<SmartCard> Cards);

/// <summary>
/// Ranks dashboard cards by how relevant they are right now, in the spirit of the watchOS Smart Stack: each rule
/// looks at the home snapshot (time, weather, presence, devices) and scores its card. The most relevant card is shown
/// large, the rest are packed onto pages of a 4 × 2 grid.
/// </summary>
public static class SmartStack
{
    public const int MinimumScore = 10;
    public const int PageCells = 8;
    public const int MaxPages = 3;

    public const string LivingRoom = "living";
    public const string DiningRoom = "dining";
    public const string Kitchen = "kitchen";
    public const string Balcony = "balcony";

    private static readonly HashSet<string> CommonAreas = [LivingRoom, DiningRoom, Kitchen, Balcony];
    private static readonly TimeSpan IdleBeforeLeftOn = TimeSpan.FromMinutes(10);

    private static readonly HashSet<CardKind> CanBeLarge =
    [
        CardKind.Rain, CardKind.Bus, CardKind.Bathroom, CardKind.WaterHeater, CardKind.Laundry, CardKind.Dishwasher,
        CardKind.LeftOn, CardKind.Climate, CardKind.ClimateSuggestion, CardKind.FrontDoor, CardKind.Energy,
        CardKind.Bedtime, CardKind.NowPlaying, CardKind.Forecast
    ];

    private static readonly HashSet<CardKind> CanBeSmall =
    [
        CardKind.Bus, CardKind.Bathroom, CardKind.WaterHeater, CardKind.Laundry, CardKind.Dishwasher, CardKind.Climate,
        CardKind.FrontDoor, CardKind.Energy, CardKind.Maintenance, CardKind.Dehumidifier, CardKind.Arrival
    ];

    public static EquatableList<CardPage> Build(HomeSnapshot home, IReadOnlySet<string> dismissed)
    {
        var cards = Rank(home, dismissed);
        return Pack(cards);
    }

    /// <summary>Every card worth showing, most relevant first, at its preferred size.</summary>
    public static List<SmartCard> Rank(HomeSnapshot home, IReadOnlySet<string> dismissed)
    {
        // The bedtime card already offers to switch off the common areas, so "left on" only lists what's left.
        var bedtime = Bedtime(home);
        var leftOn = LeftOn(home, bedtime?.Subjects ?? []);

        // The aircon card adds nothing when another card already offers to turn every running aircon off.
        var climate = Climate(home);
        var offered = (leftOn?.Subjects ?? []).Concat(bedtime?.Subjects ?? []).ToHashSet();
        if (climate is { Kind: CardKind.Climate, Subjects: { } running } && running.All(offered.Contains))
        {
            climate = null;
        }

        SmartCard?[] candidates =
        [
            Rain(home),
            Bus(home),
            Bathroom(home),
            WaterHeater(home),
            Appliance(home, ApplianceKind.WashingMachine),
            Appliance(home, ApplianceKind.Dishwasher),
            leftOn,
            climate,
            FrontDoor(home),
            Energy(home),
            bedtime,
            NowPlaying(home),
            Maintenance(home),
            Dehumidifier(home),
            Arrival(home),
            Forecast(home)
        ];

        return candidates
            .OfType<SmartCard>()
            .Where(c => c.Score >= MinimumScore && !dismissed.Contains(c.Key))
            .OrderByDescending(c => c.Score)
            .ThenBy(c => c.Kind)
            .ToList();
    }

    /// <summary>
    /// Lays cards out on pages of <see cref="PageCells"/> cells. The top card grows to large. Each card goes on the
    /// first page with room for it, shrinking to small if that is all the room left, so a gap near the top is filled
    /// by a later, smaller card rather than pushing everything down. Spare cells then let cards grow back.
    /// Large cards go first on each page so CSS grid dense packing always finds them a 2 × 2 slot.
    /// </summary>
    public static EquatableList<CardPage> Pack(IReadOnlyList<SmartCard> ranked)
    {
        var pages = new List<List<SmartCard>>();

        for (var i = 0; i < ranked.Count; i++)
        {
            var card = ranked[i];
            var size = i == 0 && CanBeLarge.Contains(card.Kind) ? CardSize.Large : card.Size;

            var placed = false;
            foreach (var page in pages)
            {
                var free = PageCells - page.Sum(c => (int)c.Size);
                var fitted = (int)size <= free ? size
                    : free >= 1 && size == CardSize.Medium && CanBeSmall.Contains(card.Kind) ? CardSize.Small
                    : (CardSize?)null;

                if (fitted is { } fits)
                {
                    page.Add(card with { Size = fits });
                    placed = true;
                    break;
                }
            }

            if (!placed && pages.Count < MaxPages)
            {
                pages.Add([card with { Size = size }]);
            }
        }

        foreach (var page in pages)
        {
            GrowIntoSpareCells(page, PageCells - page.Sum(c => (int)c.Size));
        }

        return pages
            .Select(p => new CardPage(p.OrderByDescending(c => c.Size == CardSize.Large).ToEquatableList()))
            .ToEquatableList();
    }

    private static void GrowIntoSpareCells(List<SmartCard> page, int free)
    {
        var grew = true;
        while (free > 0 && grew)
        {
            grew = false;
            for (var i = 0; i < page.Count && free > 0; i++)
            {
                var card = page[i];
                var larges = page.Count(c => c.Size == CardSize.Large);

                if (card.Size == CardSize.Small)
                {
                    page[i] = card with { Size = CardSize.Medium };
                    free -= 1;
                    grew = true;
                }
                else if (card.Size == CardSize.Medium && free >= 2 && larges < 2 && CanBeLarge.Contains(card.Kind))
                {
                    page[i] = card with { Size = CardSize.Large };
                    free -= 2;
                    grew = true;
                }
            }
        }
    }

    private static bool Between(TimeSpan time, int fromHour, int fromMinute, int toHour, int toMinute)
    {
        var from = new TimeSpan(fromHour, fromMinute, 0);
        var to = new TimeSpan(toHour, toMinute, 0);
        return from <= to ? time >= from && time < to : time >= from || time < to;
    }

    private static bool IsWeekday(DateTime now) => now.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);

    private static SmartCard? Rain(HomeSnapshot home)
    {
        if (home.Weather is not { } weather)
        {
            return null;
        }

        var chance = weather.RainChanceWithin(home.Now, TimeSpan.FromHours(2));
        if (!weather.IsRaining && chance < 40)
        {
            return null;
        }

        var score = weather.IsRaining ? 66 : chance >= 70 ? 72 : chance >= 55 ? 58 : 34;
        score += home.Blinds.AnyOpen ? 16 : -20;

        var reason = weather.IsRaining ? "正在下雨" : $"两小时内降雨概率 {chance}%";
        return new SmartCard("rain", CardKind.Rain, score, CardSize.Medium, reason);
    }

    private static SmartCard? Bus(HomeSnapshot home)
    {
        var next = home.Buses
            .SelectMany(b => b.Arrivals)
            .Where(a => a.At >= home.Now.AddMinutes(-1))
            .Select(a => (DateTime?)a.At)
            .Min();

        if (next is null)
        {
            return null;
        }

        var time = home.Now.TimeOfDay;
        var score = 24;
        string? reason = null;

        if (IsWeekday(home.Now) && Between(time, 6, 15, 9, 30))
        {
            score += 42;
            reason = "上学上班时间";
        }
        else if (Between(time, 7, 30, 10, 30))
        {
            score += 14;
        }

        if (home.HallwayOccupied)
        {
            score += 30;
            reason = "有人在玄关";
        }
        else if (home.FrontDoor.LastChanged is { } doorChanged && home.Now - doorChanged <= TimeSpan.FromMinutes(3))
        {
            score += 18;
            reason = "前门刚开过";
        }

        var wait = next.Value - home.Now;
        if (wait >= TimeSpan.FromMinutes(2) && wait <= TimeSpan.FromMinutes(9))
        {
            score += 4;
        }

        return new SmartCard("bus", CardKind.Bus, Math.Min(score, 96), CardSize.Medium, reason);
    }

    private static SmartCard? Bathroom(HomeSnapshot home)
    {
        if (home.Bathrooms.Count == 0)
        {
            return null;
        }

        if (home.Bathrooms.FirstOrDefault(b => b.State == BathroomState.Showering) is { } showering)
        {
            return new SmartCard("bathroom", CardKind.Bathroom, 60, CardSize.Medium, $"{showering.Name}有人在洗澡");
        }

        return home.Bathrooms.Any(b => b.State == BathroomState.Occupied)
            ? new SmartCard("bathroom", CardKind.Bathroom, 34, CardSize.Small)
            : new SmartCard("bathroom", CardKind.Bathroom, 14, CardSize.Small);
    }

    private static SmartCard? WaterHeater(HomeSnapshot home)
    {
        var heater = home.WaterHeater;
        if (heater.On)
        {
            return new SmartCard("water-heater", CardKind.WaterHeater, 74, CardSize.Medium);
        }

        if (heater.InventoryMinutes < WaterHeaterInventory.RunoutFloorMinutes)
        {
            return new SmartCard("water-heater", CardKind.WaterHeater, 56, CardSize.Medium, "热水快用完了");
        }

        if (Between(home.Now.TimeOfDay, 17, 30, 23, 0) && heater.InventoryMinutes < 8)
        {
            return new SmartCard("water-heater", CardKind.WaterHeater, 46, CardSize.Medium, "洗澡高峰前热水偏少");
        }

        return new SmartCard("water-heater", CardKind.WaterHeater, 16, CardSize.Small);
    }

    private static SmartCard? Appliance(HomeSnapshot home, ApplianceKind kind)
    {
        if (home.Appliances.FirstOrDefault(a => a.Kind == kind) is not { } appliance)
        {
            return null;
        }

        var cardKind = kind == ApplianceKind.WashingMachine ? CardKind.Laundry : CardKind.Dishwasher;
        var washer = kind == ApplianceKind.WashingMachine;

        if (appliance is { Status: ApplianceStatus.Finished, FinishedAt: { } finishedAt })
        {
            var since = home.Now - finishedAt;
            if (since > TimeSpan.FromHours(3))
            {
                return null;
            }

            var fresh = since <= TimeSpan.FromHours(1);
            var score = washer ? fresh ? 86 : 70 : fresh ? 58 : 44;
            return new SmartCard(
                $"{(washer ? "laundry" : "dishes")}-done:{finishedAt:yyyyMMddHHmm}",
                cardKind,
                score,
                CardSize.Medium,
                washer ? "洗衣机已停止" : "洗碗机已停止",
                DismissFor: TimeSpan.FromHours(4));
        }

        return appliance.Status == ApplianceStatus.Running
            ? new SmartCard(washer ? "laundry" : "dishes", cardKind, washer ? 42 : 26, washer ? CardSize.Medium : CardSize.Small)
            : null;
    }

    private static SmartCard? LeftOn(HomeSnapshot home, IReadOnlyCollection<string> offeredElsewhere)
    {
        var nobodyHome = home.NobodyHome;
        var devices = home.Devices
            .Where(d => d.On && !offeredElsewhere.Contains(d.EntityId))
            .Where(d => nobodyHome || IdleInEmptyRoom(home, d))
            .ToList();

        if (devices.Count == 0)
        {
            return null;
        }

        string reason;
        if (nobodyHome)
        {
            reason = "家里没人";
        }
        else
        {
            var rooms = devices
                .Select(d => home.Room(d.RoomId)?.Name)
                .OfType<string>()
                .Distinct()
                .Take(2);
            reason = $"{string.Join("、", rooms)}没人";
        }

        var ids = devices.Select(d => d.EntityId).Order().ToEquatableList();
        var score = nobodyHome ? 92 : Math.Min(64 + 3 * devices.Count, 78);

        return new SmartCard(
            $"left-on:{string.Join(',', ids)}",
            CardKind.LeftOn,
            score,
            CardSize.Medium,
            reason,
            ids,
            TimeSpan.FromHours(1));
    }

    private static bool IdleInEmptyRoom(HomeSnapshot home, DeviceInfo device)
    {
        if (home.Room(device.RoomId) is not { Occupied: false, VacantSince: { } vacantSince })
        {
            return false;
        }

        // Bedrooms are cooled before bed with nobody in them yet; that's not forgetting to turn the aircon off.
        if (device.Kind == DeviceKind.Aircon && !CommonAreas.Contains(device.RoomId) &&
            Between(home.Now.TimeOfDay, 19, 0, 2, 0))
        {
            return false;
        }

        return home.Now - vacantSince >= IdleBeforeLeftOn &&
               (device.OnSince is not { } onSince || home.Now - onSince >= IdleBeforeLeftOn);
    }

    private static SmartCard? Climate(HomeSnapshot home)
    {
        var running = home.Devices.Where(d => d is { Kind: DeviceKind.Aircon, On: true }).ToList();
        if (running.Count > 0)
        {
            return new SmartCard(
                "climate",
                CardKind.Climate,
                28 + 4 * running.Count,
                CardSize.Medium,
                Subjects: running.Select(d => d.EntityId).ToEquatableList());
        }

        if (home.Room(LivingRoom) is { Occupied: true } &&
            home.Indoor.Temperature is >= 29.5 and var temperature &&
            Between(home.Now.TimeOfDay, 9, 0, 23, 30))
        {
            return new SmartCard(
                "climate-suggestion",
                CardKind.ClimateSuggestion,
                52,
                CardSize.Medium,
                $"客厅 {temperature:0.#}°，有人在",
                home.Devices.Where(d => d is { Kind: DeviceKind.Aircon, RoomId: LivingRoom }).Select(d => d.EntityId)
                    .ToEquatableList(),
                TimeSpan.FromHours(2));
        }

        return null;
    }

    private static SmartCard? FrontDoor(HomeSnapshot home)
    {
        if (home.FrontDoor.LastChanged is not { } changed)
        {
            return null;
        }

        var since = home.Now - changed;
        if (home.FrontDoor.Open)
        {
            return since >= TimeSpan.FromMinutes(1)
                ? new SmartCard("door-open", CardKind.FrontDoor, 90, CardSize.Medium, "前门没关")
                : new SmartCard("door-opening", CardKind.FrontDoor, 45, CardSize.Small);
        }

        return since <= TimeSpan.FromMinutes(5)
            ? new SmartCard($"door:{changed:yyyyMMddHHmmss}", CardKind.FrontDoor, 40, CardSize.Small,
                DismissFor: TimeSpan.FromMinutes(10))
            : null;
    }

    private static SmartCard Energy(HomeSnapshot home) => home.Energy.TotalWatts switch
    {
        >= 3500 => new SmartCard("energy", CardKind.Energy, 60, CardSize.Medium, "用电量偏高"),
        >= 2000 => new SmartCard("energy", CardKind.Energy, 28, CardSize.Medium),
        _ => new SmartCard("energy", CardKind.Energy, 12, CardSize.Small)
    };

    private static SmartCard? Bedtime(HomeSnapshot home)
    {
        var time = home.Now.TimeOfDay;
        if (!Between(time, 22, 30, 2, 0))
        {
            return null;
        }

        var on = home.Devices.Where(d => d.On && CommonAreas.Contains(d.RoomId)).ToList();
        if (on.Count == 0)
        {
            return null;
        }

        var score = Between(time, 23, 30, 2, 0) ? 66 : 50;
        if (home.Room(LivingRoom) is { Occupied: true })
        {
            score -= 14;
        }

        // One card per night: the night started six hours before midnight.
        var night = home.Now.AddHours(-6).ToString("yyyyMMdd");
        return new SmartCard(
            $"bedtime:{night}",
            CardKind.Bedtime,
            score,
            CardSize.Medium,
            $"公共区域还有 {on.Count} 个设备开着",
            on.Select(d => d.EntityId).Order().ToEquatableList(),
            TimeSpan.FromMinutes(45));
    }

    private static SmartCard? NowPlaying(HomeSnapshot home)
    {
        if (home.Media is not { Title: not null } media)
        {
            return null;
        }

        return media.IsPlaying
            ? new SmartCard("media", CardKind.NowPlaying, 54, CardSize.Medium)
            : media.State == "paused"
                ? new SmartCard("media", CardKind.NowPlaying, 18, CardSize.Medium)
                : null;
    }

    private static SmartCard? Maintenance(HomeSnapshot home)
    {
        if (home.LowBatteries.Count == 0)
        {
            return null;
        }

        var key = string.Join(',', home.LowBatteries.Select(b => b.Name).Order());
        return new SmartCard($"maintenance:{key}", CardKind.Maintenance, 15, CardSize.Small,
            DismissFor: TimeSpan.FromDays(1));
    }

    private static SmartCard? Dehumidifier(HomeSnapshot home)
    {
        if (home.DehumidifierTankFullAt is not { } at || home.Now - at > TimeSpan.FromHours(12))
        {
            return null;
        }

        return new SmartCard($"dehumidifier:{at:yyyyMMddHHmm}", CardKind.Dehumidifier, 56, CardSize.Small,
            "除湿机已停机", DismissFor: TimeSpan.FromHours(12));
    }

    private static SmartCard? Arrival(HomeSnapshot home)
    {
        var arrivals = home.People
            .Where(p => p is { Home: true, Since: { } since } &&
                        home.Now - since <= TimeSpan.FromMinutes(10) &&
                        home.Now - since >= TimeSpan.FromMinutes(-1))
            .ToList();

        if (arrivals.Count == 0)
        {
            return null;
        }

        var key = string.Join(',', arrivals.Select(p => $"{p.Id}@{p.Since:HHmm}"));
        return new SmartCard($"arrival:{key}", CardKind.Arrival, 46, CardSize.Small,
            Subjects: arrivals.Select(p => p.Id).ToEquatableList(), DismissFor: TimeSpan.FromMinutes(15));
    }

    private static SmartCard? Forecast(HomeSnapshot home)
    {
        if (home.Weather is not { Hours.Count: > 0 })
        {
            return null;
        }

        var score = Between(home.Now.TimeOfDay, 6, 0, 9, 30) ? 40 : 20;
        return new SmartCard("forecast", CardKind.Forecast, score, CardSize.Medium);
    }
}
