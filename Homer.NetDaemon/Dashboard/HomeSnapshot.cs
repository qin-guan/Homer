using Homer.NetDaemon.Services;

namespace Homer.NetDaemon.Dashboard;

/// <summary>
/// Everything the dashboard knows about the home at one moment, read from Home Assistant and Homer's own services by
/// the SmartDashboard app. Plain data with value equality: <see cref="SmartStack"/> ranks cards from it, and the
/// Blazor page renders it. Times are local unless the name says Utc.
/// </summary>
public sealed record HomeSnapshot
{
    /// <summary>Local time the snapshot was taken, truncated to the minute so it only changes once a minute.</summary>
    public required DateTime Now { get; init; }

    public DayPhase Phase { get; init; }

    public WeatherInfo? Weather { get; init; }

    public IndoorClimate Indoor { get; init; } = new(null, null);

    public EquatableList<PersonInfo> People { get; init; } = [];

    public EquatableList<RoomInfo> Rooms { get; init; } = [];

    /// <summary>Lights, fans and aircons that can be left running in an empty room.</summary>
    public EquatableList<DeviceInfo> Devices { get; init; } = [];

    /// <summary>Living room controls shown in the dock.</summary>
    public EquatableList<QuickToggle> QuickToggles { get; init; } = [];

    public BlindsInfo Blinds { get; init; } = new([0, 0, 0]);

    public WaterHeaterInfo WaterHeater { get; init; } = new();

    public EquatableList<BathroomInfo> Bathrooms { get; init; } = [];

    public EquatableList<ApplianceInfo> Appliances { get; init; } = [];

    public EnergyInfo Energy { get; init; } = new(0, []);

    public EquatableList<BusInfo> Buses { get; init; } = [];

    public DoorInfo FrontDoor { get; init; } = new(false, null);

    public MediaInfo? Media { get; init; }

    public EquatableList<LowBatteryInfo> LowBatteries { get; init; } = [];

    /// <summary>When the dehumidifier last reported a full tank while it is still off, otherwise null.</summary>
    public DateTime? DehumidifierTankFullAt { get; init; }

    /// <summary>晾衣模式: living room fan and light automations are paused while clothes dry.</summary>
    public bool LaundryMode { get; init; }

    /// <summary>Someone is standing in the hallway by the front door, usually about to head out.</summary>
    public bool HallwayOccupied { get; init; }

    /// <summary>
    /// No tracked person is home and no room senses anyone. People without a tracked phone still count through room
    /// presence, so this stays false while anyone is moving around.
    /// </summary>
    public bool NobodyHome =>
        People.Count > 0 && People.All(p => !p.Home) && Rooms.All(r => !r.Occupied);

    public RoomInfo? Room(string id) => Rooms.FirstOrDefault(r => r.Id == id);
}

public enum DayPhase
{
    LateNight,
    Dawn,
    Morning,
    Afternoon,
    Dusk,
    Night
}

public sealed record WeatherInfo(
    double? Temperature,
    double? FeelsLike,
    double? Humidity,
    int Code,
    bool IsDay,
    EquatableList<HourForecast> Hours)
{
    public bool IsRaining => WeatherText.IsRainy(Code);

    /// <summary>Highest chance of rain among the forecast hours that start within <paramref name="window"/>.</summary>
    public int RainChanceWithin(DateTime now, TimeSpan window) =>
        Hours.Where(h => h.Time >= now.AddHours(-1) && h.Time < now + window)
            .Select(h => h.PrecipitationProbability ?? 0)
            .DefaultIfEmpty(0)
            .Max();
}

public sealed record HourForecast(DateTime Time, double? Temperature, int? PrecipitationProbability, int? Code);

public sealed record IndoorClimate(double? Temperature, double? Humidity);

public sealed record PersonInfo(string Id, string Name, bool Home, DateTime? Since)
{
    public string Initials =>
        string.Concat(Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(w => char.ToUpperInvariant(w[0])));
}

/// <param name="VacantSince">When the last presence sensor in the room cleared, while the room is empty.</param>
public sealed record RoomInfo(string Id, string Name, bool Occupied, DateTime? VacantSince);

public enum DeviceKind
{
    Light,
    Fan,
    Aircon
}

/// <param name="Detail">Short live reading, such as an aircon's mode and room temperature.</param>
public sealed record DeviceInfo(
    string EntityId,
    string Name,
    string RoomId,
    DeviceKind Kind,
    bool On,
    DateTime? OnSince,
    double? Temperature = null,
    double? TargetTemperature = null,
    string? Mode = null);

public enum QuickToggleKind
{
    Toggle,
    Blinds,
    WaterHeater
}

public enum Tint
{
    Amber,
    Ice,
    Mint,
    Sky,
    Ember
}

public sealed record QuickToggle(
    string Key,
    string Label,
    string Icon,
    QuickToggleKind Kind,
    bool On,
    bool Available,
    Tint Tint,
    string? Detail = null);

/// <param name="Positions">Each blind's position, 0 fully up (open) to 3 fully down (closed).</param>
public sealed record BlindsInfo(EquatableList<double> Positions)
{
    public const double Closed = 3.0;

    public bool AnyOpen => Positions.Any(p => p < Closed - 0.1);

    /// <summary>How far the blinds are down on average, 0–100.</summary>
    public int PercentClosed => Positions.Count == 0 ? 0 : (int)Math.Round(Positions.Average() / Closed * 100);
}

public sealed record WaterHeaterInfo
{
    public bool On { get; init; }

    public DateTime? TurnOffAtUtc { get; init; }

    public DateTime? LastOnUtc { get; init; }

    /// <summary>Estimated usable heat in the tank, in minutes of element time.</summary>
    public double InventoryMinutes { get; init; }

    public double CapacityMinutes { get; init; } = WaterHeaterInventory.CapacityMinutes;

    public double? BudgetLeftMinutes { get; init; }

    public double InventoryFraction => CapacityMinutes <= 0 ? 0 : Math.Clamp(InventoryMinutes / CapacityMinutes, 0, 1);
}

public sealed record BathroomInfo(BathroomId Id, string Name, BathroomState State, DateTime? Since);

public enum ApplianceKind
{
    WashingMachine,
    Dishwasher
}

public enum ApplianceStatus
{
    Idle,
    Running,
    Finished
}

public sealed record ApplianceInfo(
    ApplianceKind Kind,
    string Name,
    ApplianceStatus Status,
    DateTime? StartedAt,
    DateTime? FinishedAt,
    double? Watts);

/// <param name="EntityId">The device the meter belongs to where the dashboard follows it too, such as an aircon.</param>
public sealed record EnergyDevice(string Name, double Watts, string? EntityId = null);

public sealed record EnergyPart(string Name, double Watts)
{
    /// <summary>The part for every aircon's compressor, which the breakdown lists unit by unit even when idle.</summary>
    public const string Aircons = "空调";

    /// <summary>The meters behind this part that are drawing power, highest first.</summary>
    public EquatableList<EnergyDevice> Devices { get; init; } = [];
}

public sealed record EnergyInfo(double TotalWatts, EquatableList<EnergyPart> Parts);

/// <param name="Load">LTA crowding: SEA seats available, SDA standing available, LSD limited standing.</param>
public sealed record BusArrival(DateTime At, string? Load);

public sealed record BusInfo(string Service, string Stop, EquatableList<BusArrival> Arrivals);

public sealed record DoorInfo(bool Open, DateTime? LastChanged);

public sealed record MediaInfo(string EntityId, string State, string? Title, string? Artist, string? App)
{
    public bool IsPlaying => State == "playing";
}

public sealed record LowBatteryInfo(string Name, double Percent);
