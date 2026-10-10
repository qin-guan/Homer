using System.Globalization;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.Json;
using Homer.NetDaemon.Dashboard;
using Homer.NetDaemon.Entities;
using Homer.NetDaemon.Helpers;
using Homer.NetDaemon.Services;
using Homer.NetDaemon.Services.OpenMeteo;
using NetDaemon.AppModel;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace Homer.NetDaemon.Apps.Dashboard;

/// <summary>
/// Reads the home into a <see cref="HomeSnapshot"/> for the living room dashboard and publishes it to
/// <see cref="DashboardState"/>, which ranks the cards. Rebuilt when a watched entity or Homer service changes,
/// at most once a second, and on a timer so time-based relevance moves on. Also follows washing machine and
/// dishwasher cycles, which no single entity reports.
/// </summary>
[NetDaemonApp]
public sealed class SmartDashboard : IDisposable
{
    private static readonly TimeSpan PublishInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(20);
    private const double LowBatteryPercent = 15;
    private const double LowTonerPercent = 10;

    private static readonly (string Stop, string Service)[] BusServices = [("53139", "74"), ("53131", "54")];

    private sealed record Room(string Id, string Name, Entity[] Presence);

    private sealed record Device(Entity Entity, string Name, string RoomId, DeviceKind Kind, NumericSensorEntity? Temperature = null);

    private sealed record Toggle(Entity Entity, string Label, string Icon, Tint Tint, NumericSensorEntity? Temperature = null);

    /// <param name="Device">The device the meter measures, when the dashboard also follows its state.</param>
    private sealed record EnergyMeter(string Name, NumericSensorEntity Sensor, Entity? Device = null);

    private sealed record EnergySource(string Name, EnergyMeter[] Meters);

    private sealed record Battery(string Name, NumericSensorEntity Sensor, double Threshold);

    private readonly ILogger<SmartDashboard> _logger;
    private readonly DashboardState _state;
    private readonly BathroomStatusService _bathroomStatus;
    private readonly WaterHeaterTimerService _waterHeaterTimer;
    private readonly WaterHeaterInventory _inventory;

    private readonly SunEntity _sun;
    private readonly WeatherEntity _haWeather;
    private readonly NumericSensorEntity _indoorTemperature;
    private readonly NumericSensorEntity _indoorHumidity;
    private readonly BinarySensorEntity _frontDoor;
    private readonly BinarySensorEntity _hallway;
    private readonly InputBooleanEntity _laundryMode;
    private readonly InputTextEntity _blinds;
    private readonly SwitchEntity _waterHeaterSwitch;
    private readonly InputNumberEntity _waterHeaterBudget;
    private readonly NumericSensorEntity _washerPower;
    private readonly NumericSensorEntity _dishwasherPower;
    private readonly MediaPlayerEntity _media;
    private readonly EventEntity _dehumidifierTankFull;
    private readonly HumidifierEntity _dehumidifier;
    private readonly PersonEntity[] _people;
    private readonly Room[] _rooms;
    private readonly Device[] _devices;
    private readonly Toggle[] _toggles;
    private readonly EnergySource[] _energy;
    private readonly Battery[] _batteries;

    private readonly object _gate = new();
    private readonly ApplianceCycleTracker _washer = ApplianceCycleTracker.WashingMachine();
    private readonly ApplianceCycleTracker _dishwasher = ApplianceCycleTracker.Dishwasher();
    private readonly Dictionary<BathroomId, DateTime> _bathroomSince = [];
    private readonly Dictionary<string, BusInfo> _buses = [];
    private readonly Subject<Unit> _refresh = new();
    private readonly List<IDisposable> _subscriptions = [];
    private WeatherInfo? _weather;
    private volatile bool _watched;

    public SmartDashboard(
        ILogger<SmartDashboard> logger,
        IHaContext haContext,
        IScheduler scheduler,
        DashboardState state,
        BathroomStatusService bathroomStatus,
        WaterHeaterTimerService waterHeaterTimer,
        WaterHeaterInventory inventory,
        ApiObservableFactoryService api,
        BinarySensorEntities binarySensors,
        SensorEntities sensors,
        SwitchEntities switches,
        LightEntities lights,
        FanEntities fans,
        ClimateEntities climates,
        InputBooleanEntities inputBooleans,
        InputNumberEntities inputNumbers,
        InputTextEntities inputTexts,
        PersonEntities people,
        MediaPlayerEntities mediaPlayers,
        EventEntities events,
        HumidifierEntities humidifiers,
        SunEntities sun,
        WeatherEntities weather)
    {
        _logger = logger;
        _state = state;
        _bathroomStatus = bathroomStatus;
        _waterHeaterTimer = waterHeaterTimer;
        _inventory = inventory;

        _sun = sun.Sun;
        _haWeather = weather.ForecastHome;
        _indoorTemperature = sensors.LivingRoomRemoteTemperature;
        _indoorHumidity = sensors.LivingRoomRemoteHumidity;
        _frontDoor = binarySensors.FrontDoorContact;
        _hallway = binarySensors.PresenceSensorFp2B4c4PresenceSensor7;
        _laundryMode = inputBooleans.LiangYiMoShi;
        _blinds = inputTexts.BalconyBlindsState;
        _waterHeaterSwitch = switches.WaterHeaterSwitch;
        _waterHeaterBudget = inputNumbers.WaterHeaterMinutesLeft;
        _washerPower = sensors.WashingMachineCurrentConsumption;
        _dishwasherPower = sensors.DishwasherPlugCurrentConsumption;
        _media = mediaPlayers.LivingRoom;
        _dehumidifierTankFull = events.DmakerCn70515807922lTankFullE71;
        _dehumidifier = humidifiers.DmakerCn70515807922l;

        // person.homer is Homer itself, not someone who lives here.
        _people = [people.QinGuan, people.QinBo, people.GuanXiuji, people.QinXin, people.XieRongYin];

        _rooms =
        [
            new(SmartStack.LivingRoom, "客厅",
            [
                binarySensors.PresenceSensorFp2B4c4PresenceSensor1, binarySensors.PresenceSensorFp2B4c4PresenceSensor2,
                binarySensors.PresenceSensorFp2B4c4PresenceSensor3, binarySensors.PresenceSensorFp2B4c4PresenceSensor4,
                binarySensors.PresenceSensorFp2B4c4PresenceSensor7
            ]),
            new(SmartStack.DiningRoom, "餐厅", [binarySensors.PresenceSensorFp2B4c4PresenceSensor5]),
            new(SmartStack.Kitchen, "厨房",
                [binarySensors.ScreekHumanSensor2a872668AnyPresence, binarySensors.PresenceSensorFp2B4c4PresenceSensor6]),
            new(SmartStack.Balcony, "阳台",
                [binarySensors.ScreekHumanSensor2aD15cf4AnyPresence, binarySensors.PresenceSensorFp2B4c4PresenceSensor8]),
            new("master", "主卧", [binarySensors.PresenceSensorFp25939PresenceSensor1]),
            new("bedroom4", "卧室四", [inputBooleans.Bedroom4Presence])
        ];

        // Only lights known to be wired to a relay of their own: some wall switch channels run decoupled and stay on.
        _devices =
        [
            new(lights.LivingRoomKdk, "客厅灯", SmartStack.LivingRoom, DeviceKind.Light),
            new(fans.LivingRoomKdk, "客厅风扇", SmartStack.LivingRoom, DeviceKind.Fan),
            new(switches.DiningTableLights, "餐桌灯", SmartStack.DiningRoom, DeviceKind.Light),
            new(switches.KitchenLightsRight, "厨房灯", SmartStack.Kitchen, DeviceKind.Light),
            new(switches.KitchenLightsLeft, "厨房夜灯", SmartStack.Kitchen, DeviceKind.Light),
            new(fans.DmakerSg4682300181cS2Fan, "厨房风扇", SmartStack.Kitchen, DeviceKind.Fan),
            new(switches.BalconyLights, "阳台灯", SmartStack.Balcony, DeviceKind.Light),
            new(climates.Daikinap59921, "客厅空调一", SmartStack.LivingRoom, DeviceKind.Aircon, sensors.Daikinap59921InsideTemperature),
            new(climates.Daikinap16703, "客厅空调二", SmartStack.LivingRoom, DeviceKind.Aircon, sensors.Daikinap16703InsideTemperature),
            new(climates.Daikinap79207, "主卧空调", "master", DeviceKind.Aircon, sensors.Daikinap79207InsideTemperature),
            new(climates.Daikinap97235, "卧室四空调", "bedroom4", DeviceKind.Aircon, sensors.Daikinap97235InsideTemperature),
            new(climates.Daikinap35095, "卧室二空调", "bedroom2", DeviceKind.Aircon, sensors.Daikinap35095InsideTemperature),
            new(climates.Daikinap25067, "卧室三空调", "bedroom3", DeviceKind.Aircon, sensors.Daikinap25067InsideTemperature)
        ];

        _toggles =
        [
            new(lights.LivingRoomKdk, "客厅灯", "fa-lightbulb", Tint.Amber),
            new(switches.DiningTableLights, "餐桌灯", "fa-utensils", Tint.Amber),
            new(switches.BalconyLights, "阳台灯", "fa-lightbulb", Tint.Amber),
            new(switches.Daikinap59921None, "空调一", "fa-snowflake", Tint.Ice, sensors.Daikinap59921InsideTemperature),
            new(switches.Daikinap16703None, "空调二", "fa-snowflake", Tint.Ice, sensors.Daikinap16703InsideTemperature),
            new(fans.LivingRoomKdk, "风扇", "fa-fan", Tint.Mint)
        ];

        _energy =
        [
            new(EnergyPart.Aircons,
            [
                new("客厅空调一", sensors.Daikinap59921CompressorEstimatedPowerConsumption, climates.Daikinap59921),
                new("客厅空调二", sensors.Daikinap16703CompressorEstimatedPowerConsumption, climates.Daikinap16703),
                new("主卧空调", sensors.Daikinap79207CompressorEstimatedPowerConsumption, climates.Daikinap79207),
                new("卧室四空调", sensors.Daikinap97235CompressorEstimatedPowerConsumption, climates.Daikinap97235),
                new("卧室二空调", sensors.Daikinap35095CompressorEstimatedPowerConsumption, climates.Daikinap35095),
                new("卧室三空调", sensors.Daikinap25067CompressorEstimatedPowerConsumption, climates.Daikinap25067)
            ]),
            new("热水器", [new("热水器", sensors.WaterHeaterSwitchPower)]),
            new("洗衣机", [new("洗衣机", sensors.WashingMachineCurrentConsumption)]),
            new("洗碗机", [new("洗碗机", sensors.DishwasherPlugCurrentConsumption)]),
            new("插座",
            [
                new("卧室二插座", sensors.Bedroom2IkeaPlugPower), new("卧室三插座", sensors.Bedroom3IkeaPlugPower),
                new("卧室四插座", sensors.Bedroom4IkeaPlugPower), new("主卧插座", sensors.MasterBedroomIkeaPlugPower),
                new("客厅插座", sensors.LivingRoomIkeaPlugPower), new("Wi-Fi 插座", sensors.SmartWiFiPlugPower)
            ])
        ];

        _batteries =
        [
            new("前门传感器", sensors.FrontDoorBattery, LowBatteryPercent),
            new("浴室门", sensors.BathroomDoorBattery, LowBatteryPercent),
            new("浴室人体", sensors.BathroomMotionBattery, LowBatteryPercent),
            new("浴室门口人体", sensors.BathroomDoorMotionBattery, LowBatteryPercent),
            new("浴室洗手台", sensors.BathroomSinkMotionBattery, LowBatteryPercent),
            new("浴室存在", sensors.BathroomTuyaPresenceBattery, LowBatteryPercent),
            new("主浴室门", sensors.MasterBathroomDoorBattery, LowBatteryPercent),
            new("主浴室洗手台", sensors.MasterBathroomSinkMotionBattery, LowBatteryPercent),
            new("主浴室马桶", sensors.MasterBathroomToiletMotionBattery, LowBatteryPercent),
            new("主卧门", sensors.MasterBedroomDoorBattery, LowBatteryPercent),
            new("卧室四门", sensors.Bedroom4DoorBattery, LowBatteryPercent),
            new("窗帘一", sensors.BalconyBlind1MidContactBattery, LowBatteryPercent),
            new("窗帘二", sensors.BalconyBlind2MidContactBattery, LowBatteryPercent),
            new("窗帘三", sensors.BalconyBlind3MidContactBattery, LowBatteryPercent),
            new("打印机墨粉", sensors.DcpL2550dwBlackTonerRemaining, LowTonerPercent)
        ];

        var watched = new HashSet<string>(
            new Entity[]
                {
                    _sun, _haWeather, _indoorTemperature, _indoorHumidity, _frontDoor, _hallway, _laundryMode, _blinds,
                    _waterHeaterSwitch, _waterHeaterBudget, _media, _dehumidifierTankFull, _dehumidifier
                }
                .Concat(_people)
                .Concat(_rooms.SelectMany(r => r.Presence))
                .Concat(_devices.Select(d => d.Entity))
                .Concat(_devices.Select(d => d.Temperature).OfType<Entity>())
                .Concat(_toggles.Select(t => t.Entity))
                .Concat(_energy.SelectMany(e => e.Meters).Select(m => m.Sensor))
                .Concat(_batteries.Select(b => b.Sensor))
                .Select(e => e.EntityId));

        _bathroomStatus.StatusChanged += OnBathroomStatusChanged;
        _waterHeaterTimer.StateChanged += OnServiceStateChanged;
        _inventory.StateChanged += OnServiceStateChanged;

        // One subscription for every watched entity: NetDaemon parses each state change once per subscription.
        var entityChanges = haContext.StateAllChanges()
            .Where(e => watched.Contains(e.Entity.EntityId))
            .Select(_ => Unit.Default);

        // With nobody looking, only the timer rebuilds the snapshot; appliance cycles still follow every change.
        var ticks = Observable.Interval(TickInterval, scheduler).Select(_ => true);
        var changes = Observable.Merge(entityChanges, _refresh.Synchronize()).Select(_ => _watched);

        _subscriptions.Add(Observable.Merge(ticks, changes)
            .Buffer(PublishInterval, scheduler)
            .Where(batch => batch.Count > 0)
            .Subscribe(batch => Publish(rebuild: batch.Any(b => b))));

        _subscriptions.Add(state.HasViewers.Subscribe(watching =>
        {
            _watched = watching;
            if (watching)
            {
                _refresh.OnNext(Unit.Default);
            }
        }));

        _subscriptions.Add(api.CreateForecast().Subscribe(forecast =>
        {
            lock (_gate)
            {
                _weather = MapWeather(forecast);
            }

            _refresh.OnNext(Unit.Default);
        }));

        // DataMall is polled every few seconds, so only while a dashboard is open.
        _subscriptions.Add(state.HasViewers
            .Select(watching => watching
                ? BusServices.Select(b => BusFeed(api, b.Stop, b.Service)).Merge()
                : Observable.Empty<BusInfo>())
            .Switch()
            .Subscribe(bus =>
            {
                lock (_gate)
                {
                    _buses[bus.Service] = bus;
                }

                _refresh.OnNext(Unit.Default);
            }));

        Publish();
    }

    public void Dispose()
    {
        _bathroomStatus.StatusChanged -= OnBathroomStatusChanged;
        _waterHeaterTimer.StateChanged -= OnServiceStateChanged;
        _inventory.StateChanged -= OnServiceStateChanged;
        _subscriptions.ForEach(s => s.Dispose());

        // Completed rather than disposed: a service event already in flight may still call OnNext, which is then a no-op.
        _refresh.OnCompleted();
    }

    private void OnBathroomStatusChanged(BathroomStatusChange change)
    {
        lock (_gate)
        {
            _bathroomSince[change.Bathroom] = DateTime.Now;
        }

        _refresh.OnNext(Unit.Default);
    }

    private void OnServiceStateChanged() => _refresh.OnNext(Unit.Default);

    private void Publish(bool rebuild = true)
    {
        try
        {
            lock (_gate)
            {
                if (!rebuild)
                {
                    TrackAppliances(DateTime.Now);
                    return;
                }

                // Published under the lock so a slower, older snapshot can't land after a newer one.
                _state.Publish(BuildSnapshot());
            }
        }
        catch (Exception e)
        {
            // Entities are briefly unavailable while Home Assistant restarts; the next change or tick retries.
            _logger.LogWarning(e, "Failed to build the dashboard snapshot");
        }
    }

    private HomeSnapshot BuildSnapshot()
    {
        var precise = DateTime.Now;
        var now = new DateTime(precise.Year, precise.Month, precise.Day, precise.Hour, precise.Minute, 0, precise.Kind);
        var rooms = _rooms.Select(BuildRoom).ToEquatableList();

        var (washerWatts, dishwasherWatts) = TrackAppliances(precise);

        return new HomeSnapshot
        {
            Now = now,
            Phase = PhaseFor(precise.TimeOfDay, _sun.Attributes?.Elevation, _sun.Attributes?.Rising),
            Weather = _weather ?? WeatherFromHomeAssistant(),
            Indoor = new IndoorClimate(Round(_indoorTemperature.State, 1), Round(_indoorHumidity.State, 0)),
            People = _people.Select(BuildPerson).ToEquatableList(),
            Rooms = rooms,
            Devices = _devices.Select(BuildDevice).ToEquatableList(),
            QuickToggles = BuildQuickToggles(),
            Blinds = new BlindsInfo(ReadBlinds()),
            WaterHeater = new WaterHeaterInfo
            {
                On = _waterHeaterSwitch.IsOn(),
                TurnOffAtUtc = _waterHeaterTimer.ScheduledTurnOffDateTime,
                LastOnUtc = _waterHeaterTimer.LastTurnedOnDateTime,
                InventoryMinutes = Math.Round(_inventory.StateOfChargeMinutes, 1),
                BudgetLeftMinutes = Round(_waterHeaterBudget.State, 0)
            },
            Bathrooms =
            [
                BuildBathroom(BathroomId.Bathroom, "浴室"),
                BuildBathroom(BathroomId.MasterBathroom, "主浴室")
            ],
            Appliances =
            [
                BuildAppliance(ApplianceKind.WashingMachine, "洗衣机", _washer, washerWatts),
                BuildAppliance(ApplianceKind.Dishwasher, "洗碗机", _dishwasher, dishwasherWatts)
            ],
            Energy = BuildEnergy(),
            Buses = BusServices
                .Select(b => _buses.GetValueOrDefault(b.Service))
                .OfType<BusInfo>()
                .Select(b => b with { Arrivals = b.Arrivals.Where(a => a.At >= precise.AddMinutes(-1)).ToEquatableList() })
                .ToEquatableList(),
            FrontDoor = new DoorInfo(_frontDoor.IsOn(), Local(_frontDoor.EntityState?.LastChanged)),
            Media = BuildMedia(),
            LowBatteries = _batteries
                .Where(b => SensorMeasurements.Read(b.Sensor) is { } level && level < b.Threshold)
                .Select(b => new LowBatteryInfo(b.Name, Math.Round(SensorMeasurements.Read(b.Sensor)!.Value)))
                .ToEquatableList(),
            DehumidifierTankFullAt = _dehumidifier.State == "on" ? null : ParseTimestamp(_dehumidifierTankFull.State),
            LaundryMode = _laundryMode.IsOn(),
            HallwayOccupied = _hallway.IsOn()
        };
    }

    private (double? Washer, double? Dishwasher) TrackAppliances(DateTime now)
    {
        var washerWatts = SensorMeasurements.Read(_washerPower, toWatts: true);
        var dishwasherWatts = SensorMeasurements.Read(_dishwasherPower, toWatts: true);
        _washer.Update(now, washerWatts);
        _dishwasher.Update(now, dishwasherWatts);
        return (washerWatts, dishwasherWatts);
    }

    public static DayPhase PhaseFor(TimeSpan time, double? sunElevation, bool? sunRising)
    {
        if (time < TimeSpan.FromHours(5))
        {
            return DayPhase.LateNight;
        }

        if (sunElevation is { } elevation)
        {
            if (elevation < -4)
            {
                return time < TimeSpan.FromHours(12) ? DayPhase.LateNight : DayPhase.Night;
            }

            if (elevation < 8)
            {
                return sunRising ?? time < TimeSpan.FromHours(12) ? DayPhase.Dawn : DayPhase.Dusk;
            }

            return time < TimeSpan.FromHours(12) ? DayPhase.Morning : DayPhase.Afternoon;
        }

        return time.TotalHours switch
        {
            < 6.5 => DayPhase.LateNight,
            < 7.5 => DayPhase.Dawn,
            < 12 => DayPhase.Morning,
            < 18.5 => DayPhase.Afternoon,
            < 19.5 => DayPhase.Dusk,
            _ => DayPhase.Night
        };
    }

    private static RoomInfo BuildRoom(Room room)
    {
        var occupied = room.Presence.Any(p => p.IsOn());
        DateTime? vacantSince = null;
        if (!occupied)
        {
            vacantSince = room.Presence.Select(p => Local(p.EntityState?.LastChanged)).Max();
        }

        return new RoomInfo(room.Id, room.Name, occupied, vacantSince);
    }

    private static DeviceInfo BuildDevice(Device device)
    {
        var state = device.Entity.State;
        var on = device.Kind == DeviceKind.Aircon
            ? state is not (null or "off" or "unavailable" or "unknown")
            : state == "on";

        double? target = null;
        if (device.Kind == DeviceKind.Aircon &&
            device.Entity.EntityState?.AttributesJson is { ValueKind: JsonValueKind.Object } attributes &&
            attributes.TryGetProperty("temperature", out var temperature) &&
            temperature.ValueKind == JsonValueKind.Number)
        {
            target = temperature.GetDouble();
        }

        return new DeviceInfo(
            device.Entity.EntityId,
            device.Name,
            device.RoomId,
            device.Kind,
            on,
            on ? Local(device.Entity.EntityState?.LastChanged) : null,
            device.Temperature is null ? null : Round(SensorMeasurements.Read(device.Temperature), 1),
            target,
            device.Kind == DeviceKind.Aircon && on ? state : null);
    }

    private EquatableList<QuickToggle> BuildQuickToggles()
    {
        var toggles = _toggles
            .Select(t => new QuickToggle(
                t.Entity.EntityId,
                t.Label,
                t.Icon,
                QuickToggleKind.Toggle,
                t.Entity.State == "on",
                t.Entity.State is not (null or "unavailable" or "unknown"),
                t.Tint,
                t.Temperature is null ? null : FormatTemperature(SensorMeasurements.Read(t.Temperature))))
            .ToList();

        var blinds = new BlindsInfo(ReadBlinds());
        toggles.Add(new QuickToggle("blinds", "窗帘", "blinds", QuickToggleKind.Blinds, blinds.PercentClosed > 2, true,
            Tint.Sky, blinds.PercentClosed switch { <= 2 => "全开", >= 98 => "全关", var p => $"关 {p}%" }));

        toggles.Add(new QuickToggle("water-heater", "热水器", "fa-fire-flame-curved", QuickToggleKind.WaterHeater,
            _waterHeaterSwitch.IsOn(), _waterHeaterSwitch.State is not (null or "unavailable"), Tint.Ember,
            $"{_inventory.StateOfChargeMinutes:0} 分钟"));

        return toggles.ToEquatableList();
    }

    private PersonInfo BuildPerson(PersonEntity person) => new(
        person.EntityId,
        person.Attributes?.FriendlyName ?? person.EntityId,
        person.State == "home",
        Local(person.EntityState?.LastChanged));

    private BathroomInfo BuildBathroom(BathroomId id, string name) =>
        new(id, name, _bathroomStatus.GetStatus(id), _bathroomSince.TryGetValue(id, out var since) ? since : null);

    private static ApplianceInfo BuildAppliance(ApplianceKind kind, string name, ApplianceCycleTracker tracker, double? watts) =>
        new(kind, name, tracker.Status, tracker.StartedAt, tracker.FinishedAt, Round(watts, -1));

    /// <summary>
    /// Each part's total is rounded from its meters' sum, so standby draw that rounds away per device still counts.
    /// </summary>
    private EnergyInfo BuildEnergy()
    {
        var parts = _energy
            .Select(e =>
            {
                var readings = e.Meters
                    .Select(m => (Meter: m, Watts: SensorMeasurements.Read(m.Sensor, toWatts: true) ?? 0))
                    .ToList();

                return new EnergyPart(e.Name, Round(readings.Sum(r => r.Watts), -1) ?? 0)
                {
                    Devices = readings
                        .Select(r => new EnergyDevice(r.Meter.Name, Round(r.Watts, -1) ?? 0, r.Meter.Device?.EntityId))
                        .Where(d => d.Watts > 0)
                        .OrderByDescending(d => d.Watts)
                        .ToEquatableList()
                };
            })
            .Where(p => p.Watts > 0)
            .OrderByDescending(p => p.Watts)
            .ToEquatableList();

        return new EnergyInfo(parts.Sum(p => p.Watts), parts);
    }

    private MediaInfo? BuildMedia()
    {
        if (_media.State is not ("playing" or "paused"))
        {
            return null;
        }

        var attributes = _media.Attributes;
        return new MediaInfo(_media.EntityId, _media.State, attributes?.MediaTitle, attributes?.MediaArtist,
            attributes?.AppName);
    }

    private EquatableList<double> ReadBlinds()
    {
        try
        {
            var positions = JsonSerializer.Deserialize<List<double>>(_blinds.State ?? "[0,0,0]") ?? [0, 0, 0];
            return positions.Select(p => Math.Round(Math.Clamp(p, 0, BlindsInfo.Closed), 1)).ToEquatableList();
        }
        catch (JsonException)
        {
            return [0, 0, 0];
        }
    }

    private WeatherInfo? WeatherFromHomeAssistant()
    {
        if (_haWeather.State is null or "unavailable" or "unknown")
        {
            return null;
        }

        var attributes = _haWeather.Attributes;
        return new WeatherInfo(
            Round(attributes?.Temperature, 1),
            null,
            Round(attributes?.Humidity, 0),
            WeatherText.FromHomeAssistantCondition(_haWeather.State),
            _sun.State != "below_horizon",
            []);
    }

    private static WeatherInfo MapWeather(OpenMeteoResponse response)
    {
        var offset = TimeSpan.FromSeconds(response.UtcOffsetSeconds);
        var hours = new List<HourForecast>();

        if (response.Hourly is { } hourly)
        {
            for (var i = 0; i < hourly.Time.Count; i++)
            {
                if (!DateTime.TryParse(hourly.Time[i], CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
                {
                    continue;
                }

                hours.Add(new HourForecast(
                    new DateTimeOffset(time, offset).LocalDateTime,
                    Round(hourly.Temperature.ElementAtOrDefault(i), 0),
                    hourly.PrecipitationProbability.ElementAtOrDefault(i),
                    hourly.WeatherCode.ElementAtOrDefault(i)));
            }
        }

        var current = response.Current;
        return new WeatherInfo(
            Round(current.Temperature, 1),
            Round(current.ApparentTemperature, 0),
            Round(current.RelativeHumidity, 0),
            current.WeatherCode,
            current.IsDay != 0,
            hours.ToEquatableList());
    }

    private static IObservable<BusInfo> BusFeed(ApiObservableFactoryService api, string stop, string service) =>
        api.CreateWithBusStopCode(stop).Select(response =>
        {
            var next = response.Services?.FirstOrDefault(s => s.ServiceNo == service);
            var arrivals = next is null
                ? EquatableList<BusArrival>.Empty
                : new[] { next.NextBus, next.NextBus2, next.NextBus3 }
                    .Where(b => b?.EstimatedArrival is not null)
                    .Select(b => new BusArrival(b!.EstimatedArrival!.Value, string.IsNullOrEmpty(b.Load) ? null : b.Load))
                    .ToEquatableList();

            return new BusInfo(service, stop, arrivals);
        });

    private static DateTime? ParseTimestamp(string? value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var timestamp)
            ? timestamp.LocalDateTime
            : null;

    private static DateTime? Local(DateTime? time) => time?.ToLocalTime();

    private static double? Round(double? value, int digits) => value switch
    {
        null => null,
        var v when double.IsNaN(v.Value) => null,
        var v when digits < 0 => Math.Round(v.Value / Math.Pow(10, -digits)) * Math.Pow(10, -digits),
        var v => Math.Round(v.Value, digits)
    };

    private static string? FormatTemperature(double? temperature) => temperature is { } t ? $"{t:0.#}°" : null;
}
