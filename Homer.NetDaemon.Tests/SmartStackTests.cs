using Homer.NetDaemon.Dashboard;

namespace Homer.NetDaemon.Tests;

public class SmartStackTests
{
    private static readonly HashSet<string> NothingDismissed = [];

    private static List<SmartCard> Rank(HomeSnapshot home) => SmartStack.Rank(home, NothingDismissed);

    private static SmartCard Top(HomeSnapshot home) =>
        SmartStack.Build(home, NothingDismissed)[0].Cards[0];

    [Fact]
    public void Nobody_home_with_devices_on_leads_with_turning_them_off()
    {
        var top = Top(DemoScenarios.Create("away"));

        Assert.Equal(CardKind.LeftOn, top.Kind);
        Assert.Equal(CardSize.Large, top.Size);
        Assert.Equal("家里没人", top.Reason);
        Assert.Equal(
            ["climate.daikinap59921", "fan.living_room_kdk", "switch.balcony_lights"],
            top.Subjects!.Order());
    }

    [Fact]
    public void Aircon_card_is_dropped_when_another_card_already_offers_to_turn_it_off()
    {
        var cards = Rank(DemoScenarios.Create("away"));

        Assert.DoesNotContain(cards, c => c.Kind == CardKind.Climate);
    }

    [Fact]
    public void Rain_on_the_way_with_blinds_open_leads()
    {
        var top = Top(DemoScenarios.Create("rain"));

        Assert.Equal(CardKind.Rain, top.Kind);
    }

    [Fact]
    public void Rain_matters_less_once_the_blinds_are_closed()
    {
        var home = DemoScenarios.Create("rain");
        var open = Rank(home).Single(c => c.Kind == CardKind.Rain);
        var closed = Rank(home with { Blinds = new BlindsInfo([3, 3, 3]) }).Single(c => c.Kind == CardKind.Rain);

        Assert.True(closed.Score < open.Score);
    }

    [Fact]
    public void Someone_in_the_hallway_on_a_school_morning_brings_up_the_buses()
    {
        var home = DemoScenarios.Create("morning");

        var top = Top(home);

        Assert.Equal(CardKind.Bus, top.Kind);
        Assert.Equal("有人在玄关", top.Reason);
    }

    [Fact]
    public void Buses_without_upcoming_arrivals_are_not_shown()
    {
        var home = DemoScenarios.Create("morning") with { Buses = [] };

        Assert.DoesNotContain(Rank(home), c => c.Kind == CardKind.Bus);
    }

    [Fact]
    public void Front_door_left_open_is_urgent()
    {
        var top = Top(DemoScenarios.Create("hot"));

        Assert.Equal(CardKind.FrontDoor, top.Kind);
        Assert.Equal(90, top.Score);
    }

    [Fact]
    public void Hot_occupied_living_room_suggests_the_living_room_aircon()
    {
        var suggestion = Rank(DemoScenarios.Create("hot")).Single(c => c.Kind == CardKind.ClimateSuggestion);

        Assert.Equal("climate.daikinap59921", suggestion.Subjects![0]);
        Assert.NotNull(suggestion.DismissFor);
    }

    [Fact]
    public void Bedtime_offers_the_common_areas_and_left_on_does_not_repeat_them()
    {
        var cards = Rank(DemoScenarios.Create("bedtime"));

        var bedtime = cards.Single(c => c.Kind == CardKind.Bedtime);
        Assert.Contains("light.living_room_kdk", bedtime.Subjects!);
        Assert.DoesNotContain("climate.daikinap79207", bedtime.Subjects!);

        var leftOn = cards.SingleOrDefault(c => c.Kind == CardKind.LeftOn);
        Assert.True(leftOn?.Subjects is null || !leftOn.Subjects.Intersect(bedtime.Subjects!).Any());
    }

    [Fact]
    public void A_bedroom_being_cooled_before_bed_is_not_left_on()
    {
        var home = DemoScenarios.Create("bedtime");

        var leftOn = Rank(home).SingleOrDefault(c => c.Kind == CardKind.LeftOn);

        Assert.True(leftOn?.Subjects is null || !leftOn.Subjects.Contains("climate.daikinap79207"));
    }

    [Fact]
    public void The_same_bedroom_aircon_in_the_afternoon_is_left_on()
    {
        var bedtime = DemoScenarios.Create("bedtime");
        var afternoon = bedtime.Now.Date.AddHours(15);
        var home = bedtime with
        {
            Now = afternoon,
            Rooms = bedtime.Rooms.Select(r => r with { VacantSince = afternoon.AddHours(-1) }).ToEquatableList(),
            Devices = bedtime.Devices.Select(d => d.On ? d with { OnSince = afternoon.AddHours(-2) } : d).ToEquatableList()
        };

        var leftOn = Rank(home).Single(c => c.Kind == CardKind.LeftOn);

        Assert.Contains("climate.daikinap79207", leftOn.Subjects!);
    }

    [Fact]
    public void Showering_raises_the_bathroom_card()
    {
        var evening = Rank(DemoScenarios.Create("evening")).Single(c => c.Kind == CardKind.Bathroom);
        var morning = Rank(DemoScenarios.Create("morning")).Single(c => c.Kind == CardKind.Bathroom);

        Assert.True(evening.Score > morning.Score);
        Assert.Equal("主浴室有人在洗澡", evening.Reason);
    }

    [Fact]
    public void Finished_laundry_can_be_dismissed_and_stays_hidden()
    {
        var home = DemoScenarios.Create("rain");
        var laundry = Rank(home).Single(c => c.Kind == CardKind.Laundry);

        Assert.NotNull(laundry.DismissFor);
        Assert.DoesNotContain(SmartStack.Rank(home, new HashSet<string> { laundry.Key }), c => c.Kind == CardKind.Laundry);
    }

    [Fact]
    public void Finished_laundry_stops_showing_after_a_few_hours()
    {
        var home = DemoScenarios.Create("rain");
        var later = home with { Now = home.Now.AddHours(4) };

        Assert.DoesNotContain(Rank(later), c => c.Kind == CardKind.Laundry);
    }

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void Pages_always_fit_the_grid(string scenario)
    {
        var pages = SmartStack.Build(DemoScenarios.Create(scenario), NothingDismissed);

        Assert.InRange(pages.Count, 1, SmartStack.MaxPages);
        foreach (var page in pages)
        {
            Assert.InRange(page.Cards.Sum(c => (int)c.Size), 1, SmartStack.PageCells);
            Assert.True(page.Cards.Count(c => c.Size == CardSize.Large) <= 2);

            // Large cards first, so CSS dense packing can always give them a 2 × 2 slot.
            var firstSmaller = page.Cards.ToList().FindIndex(c => c.Size != CardSize.Large);
            Assert.True(firstSmaller < 0 || page.Cards.Skip(firstSmaller).All(c => c.Size != CardSize.Large));
        }

        Assert.Equal(CardSize.Large, pages[0].Cards[0].Size);
    }

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void Card_keys_are_unique(string scenario)
    {
        var keys = SmartStack.Build(DemoScenarios.Create(scenario), NothingDismissed)
            .SelectMany(p => p.Cards)
            .Select(c => c.Key)
            .ToList();

        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    [Fact]
    public void A_gap_on_the_first_page_is_filled_by_a_later_small_card()
    {
        SmartCard[] ranked =
        [
            new("rain", CardKind.Rain, 90, CardSize.Medium),
            new("bus", CardKind.Bus, 80, CardSize.Medium),
            new("climate", CardKind.Climate, 70, CardSize.Small),
            new("forecast", CardKind.Forecast, 60, CardSize.Medium),
            new("maintenance", CardKind.Maintenance, 15, CardSize.Small)
        ];

        var pages = SmartStack.Pack(ranked);

        // Large + medium + small leave one cell: the forecast can't shrink into it, the battery reminder can.
        Assert.Equal(["rain", "bus", "climate", "maintenance"], pages[0].Cards.Select(c => c.Key));
        Assert.Equal(CardSize.Large, pages[0].Cards[0].Size);
        Assert.Equal(["forecast"], pages[1].Cards.Select(c => c.Key));
    }

    [Fact]
    public void A_medium_card_shrinks_to_fill_the_last_cell()
    {
        SmartCard[] ranked =
        [
            new("rain", CardKind.Rain, 90, CardSize.Medium),
            new("bus", CardKind.Bus, 80, CardSize.Medium),
            new("bathroom", CardKind.Bathroom, 70, CardSize.Small),
            new("energy", CardKind.Energy, 60, CardSize.Medium)
        ];

        var page = Assert.Single(SmartStack.Pack(ranked));

        Assert.Equal(CardSize.Small, page.Cards.Single(c => c.Key == "energy").Size);
    }

    public static TheoryData<string> Scenarios() => new(DemoScenarios.Names);
}
