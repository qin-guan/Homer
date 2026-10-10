using Homer.NetDaemon.Services;

namespace Homer.NetDaemon.Dashboard;

/// <summary>
/// Sample homes for previewing the dashboard without Home Assistant (<c>/?demo=evening</c>, Development only) and for
/// tests. Times are today's date at the scenario's time of day.
/// </summary>
public static class DemoScenarios
{
    public static readonly string[] Names = ["morning", "rain", "evening", "bedtime", "away", "hot"];

    public static HomeSnapshot Create(string name)
    {
        var today = DateTime.Today;
        return name switch
        {
            "rain" => Rain(today.AddHours(15).AddMinutes(20)),
            "evening" => Evening(today.AddHours(20).AddMinutes(40)),
            "bedtime" => Bedtime(today.AddHours(23).AddMinutes(45)),
            "away" => Away(today.AddHours(14).AddMinutes(5)),
            "hot" => Hot(today.AddHours(13).AddMinutes(30)),
            _ => Morning(NextWeekday(today).AddHours(7).AddMinutes(45))
        };
    }

    public static DashboardView View(string name)
    {
        var home = Create(name);
        return new DashboardView(home, SmartStack.Build(home, new HashSet<string>()));
    }

    private static DateTime NextWeekday(DateTime date)
    {
        while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            date = date.AddDays(1);
        }

        return date;
    }

    private static HomeSnapshot Morning(DateTime now) => Base(now, DayPhase.Morning) with
    {
        Weather = Weather(now, code: 1, temperature: 27.6, rain: [5, 10, 10, 20, 25, 30, 30, 40]),
        HallwayOccupied = true,
        Bathrooms =
        [
            new BathroomInfo(BathroomId.Bathroom, "浴室", BathroomState.Occupied, now.AddMinutes(-4)),
            new BathroomInfo(BathroomId.MasterBathroom, "主浴室", BathroomState.Unoccupied, null)
        ],
        Buses = Buses(now, [3, 14, 26], [8, 19, 33])
    };

    private static HomeSnapshot Rain(DateTime now) => Base(now, DayPhase.Afternoon) with
    {
        Weather = Weather(now, code: 3, temperature: 29.1, rain: [35, 65, 85, 90, 70, 40, 20, 10]),
        Appliances =
        [
            new ApplianceInfo(ApplianceKind.WashingMachine, "洗衣机", ApplianceStatus.Finished, now.AddMinutes(-78),
                now.AddMinutes(-18), 0),
            new ApplianceInfo(ApplianceKind.Dishwasher, "洗碗机", ApplianceStatus.Idle, null, null, 0)
        ],
        Devices = Devices(now, on: ["climate.daikinap35095"]),
        Buses = Buses(now, [6, 18], [11, 24])
    };

    private static HomeSnapshot Evening(DateTime now) => Base(now, DayPhase.Night) with
    {
        Weather = Weather(now, code: 2, temperature: 27.2, rain: [10, 10, 5, 5, 5, 10, 10, 15], isDay: false),
        Bathrooms =
        [
            new BathroomInfo(BathroomId.Bathroom, "浴室", BathroomState.Unoccupied, null),
            new BathroomInfo(BathroomId.MasterBathroom, "主浴室", BathroomState.Showering, now.AddMinutes(-7))
        ],
        WaterHeater = new WaterHeaterInfo
        {
            On = true,
            TurnOffAtUtc = DateTime.UtcNow.AddMinutes(8).AddSeconds(24),
            LastOnUtc = DateTime.UtcNow.AddMinutes(-4),
            InventoryMinutes = 6.4,
            BudgetLeftMinutes = 71
        },
        Appliances =
        [
            new ApplianceInfo(ApplianceKind.WashingMachine, "洗衣机", ApplianceStatus.Idle, null, null, 0),
            new ApplianceInfo(ApplianceKind.Dishwasher, "洗碗机", ApplianceStatus.Running, now.AddMinutes(-36), null, 1870)
        ],
        Media = new MediaInfo("media_player.living_room", "playing", "晴天", "周杰伦", "Spotify"),
        Devices = Devices(now, on: ["light.living_room_kdk", "climate.daikinap59921"]),
        Energy = new EnergyInfo(5610,
        [
            new("热水器", 2980) { Devices = [new("热水器", 2980)] },
            new("洗碗机", 1870) { Devices = [new("洗碗机", 1870)] },
            new(EnergyPart.Aircons, 620) { Devices = [new("客厅空调一", 620, "climate.daikinap59921")] },
            new("插座", 140) { Devices = [new("客厅插座", 90), new("主卧插座", 50)] }
        ]),
        Buses = Buses(now, [9, 21], [4, 17])
    };

    private static HomeSnapshot Bedtime(DateTime now) => Base(now, DayPhase.Night) with
    {
        Weather = Weather(now, code: 0, temperature: 26.4, rain: [0, 0, 5, 5, 0, 0, 0, 0], isDay: false),
        Rooms = Rooms(now, occupied: [], vacantMinutes: 20),
        Devices = Devices(now, on:
        [
            "light.living_room_kdk", "switch.dining_table_lights", "switch.balcony_lights", "climate.daikinap59921",
            "climate.daikinap79207"
        ]),
        Energy = new EnergyInfo(1760,
        [
            new(EnergyPart.Aircons, 1240)
            {
                Devices = [new("客厅空调一", 760, "climate.daikinap59921"), new("主卧空调", 480, "climate.daikinap79207")]
            },
            new("插座", 520) { Devices = [new("客厅插座", 380), new("主卧插座", 140)] }
        ]),
        Buses = []
    };

    private static HomeSnapshot Away(DateTime now) => Base(now, DayPhase.Afternoon) with
    {
        Weather = Weather(now, code: 1, temperature: 31.5, rain: [10, 15, 20, 25, 20, 15, 10, 10]),
        People = People(now, home: []),
        Rooms = Rooms(now, occupied: [], vacantMinutes: 50),
        Devices = Devices(now, on: ["climate.daikinap59921", "switch.balcony_lights", "fan.living_room_kdk"]),
        FrontDoor = new DoorInfo(false, now.AddMinutes(-48)),
        Energy = new EnergyInfo(1460,
        [
            new(EnergyPart.Aircons, 1180) { Devices = [new("客厅空调一", 1180, "climate.daikinap59921")] },
            new("插座", 280) { Devices = [new("客厅插座", 160), new("卧室四插座", 120)] }
        ])
    };

    private static HomeSnapshot Hot(DateTime now) => Base(now, DayPhase.Afternoon) with
    {
        Weather = Weather(now, code: 0, temperature: 33.4, rain: [5, 5, 10, 15, 30, 45, 35, 20]),
        Indoor = new IndoorClimate(30.4, 71),
        FrontDoor = new DoorInfo(true, now.AddMinutes(-3)),
        People = People(now, home: ["person.qin_guan", "person.qin_bo", "person.guan_xiuji"], arrived: "person.qin_bo"),
        LowBatteries = [new LowBatteryInfo("前门传感器", 9), new LowBatteryInfo("窗帘二", 12)]
    };

    private static HomeSnapshot Base(DateTime now, DayPhase phase) => new()
    {
        Now = now,
        Phase = phase,
        Indoor = new IndoorClimate(27.4, 64),
        People = People(now, home: ["person.qin_guan", "person.qin_bo", "person.guan_xiuji", "person.qin_xin"]),
        Rooms = Rooms(now, occupied: [SmartStack.LivingRoom], vacantMinutes: 30),
        Devices = Devices(now, on: []),
        QuickToggles =
        [
            new QuickToggle("light.living_room_kdk", "客厅灯", "fa-lightbulb", QuickToggleKind.Toggle, phase is DayPhase.Night, true, Tint.Amber),
            new QuickToggle("switch.dining_table_lights", "餐桌灯", "fa-utensils", QuickToggleKind.Toggle, false, true, Tint.Amber),
            new QuickToggle("switch.balcony_lights", "阳台灯", "fa-lightbulb", QuickToggleKind.Toggle, false, true, Tint.Amber),
            new QuickToggle("switch.daikinap59921_none", "空调一", "fa-snowflake", QuickToggleKind.Toggle, false, true, Tint.Ice, "27.4°"),
            new QuickToggle("switch.daikinap16703_none", "空调二", "fa-snowflake", QuickToggleKind.Toggle, false, true, Tint.Ice, "27.8°"),
            new QuickToggle("fan.living_room_kdk", "风扇", "fa-fan", QuickToggleKind.Toggle, true, true, Tint.Mint),
            new QuickToggle("blinds", "窗帘", "blinds", QuickToggleKind.Blinds, false, true, Tint.Sky, "全开"),
            new QuickToggle("water-heater", "热水器", "fa-fire-flame-curved", QuickToggleKind.WaterHeater, false, true, Tint.Ember, "12 分钟")
        ],
        Blinds = new BlindsInfo([0, 0, 0.6]),
        WaterHeater = new WaterHeaterInfo
        {
            InventoryMinutes = 12.4,
            BudgetLeftMinutes = 96,
            LastOnUtc = DateTime.UtcNow.AddHours(-9)
        },
        Bathrooms =
        [
            new BathroomInfo(BathroomId.Bathroom, "浴室", BathroomState.Unoccupied, null),
            new BathroomInfo(BathroomId.MasterBathroom, "主浴室", BathroomState.Unoccupied, null)
        ],
        Appliances =
        [
            new ApplianceInfo(ApplianceKind.WashingMachine, "洗衣机", ApplianceStatus.Idle, null, null, 0),
            new ApplianceInfo(ApplianceKind.Dishwasher, "洗碗机", ApplianceStatus.Idle, null, null, 0)
        ],
        Energy = new EnergyInfo(520, [new("插座", 520) { Devices = [new("客厅插座", 380), new("主卧插座", 140)] }]),
        FrontDoor = new DoorInfo(false, now.AddHours(-2))
    };

    private static WeatherInfo Weather(DateTime now, int code, double temperature, int[] rain, bool isDay = true)
    {
        var hourStart = now.Date.AddHours(now.Hour);
        var hours = rain
            .Select((chance, i) => new HourForecast(
                hourStart.AddHours(i),
                Math.Round(temperature - i * 0.4),
                chance,
                chance >= 60 ? 63 : chance >= 40 ? 80 : code))
            .ToEquatableList();

        return new WeatherInfo(temperature, Math.Round(temperature + 4), 74, code, isDay, hours);
    }

    private static EquatableList<PersonInfo> People(DateTime now, string[] home, string? arrived = null)
    {
        (string Id, string Name)[] everyone =
        [
            ("person.qin_guan", "Qin Guan"), ("person.qin_bo", "Qin Bo"), ("person.guan_xiuji", "Guan Xiuji"),
            ("person.qin_xin", "Qin Xin"), ("person.xie_rong_yin", "Xie Rong Yin")
        ];

        return everyone
            .Select(p => new PersonInfo(p.Id, p.Name, home.Contains(p.Id),
                p.Id == arrived ? now.AddMinutes(-4) : now.AddHours(-5)))
            .ToEquatableList();
    }

    private static EquatableList<RoomInfo> Rooms(DateTime now, string[] occupied, int vacantMinutes)
    {
        (string Id, string Name)[] rooms =
        [
            (SmartStack.LivingRoom, "客厅"), (SmartStack.DiningRoom, "餐厅"), (SmartStack.Kitchen, "厨房"),
            (SmartStack.Balcony, "阳台"), ("master", "主卧"), ("bedroom4", "卧室四")
        ];

        return rooms
            .Select(r => occupied.Contains(r.Id)
                ? new RoomInfo(r.Id, r.Name, true, null)
                : new RoomInfo(r.Id, r.Name, false, now.AddMinutes(-vacantMinutes)))
            .ToEquatableList();
    }

    private static EquatableList<DeviceInfo> Devices(DateTime now, string[] on)
    {
        DeviceInfo[] devices =
        [
            new("light.living_room_kdk", "客厅灯", SmartStack.LivingRoom, DeviceKind.Light, false, null),
            new("fan.living_room_kdk", "客厅风扇", SmartStack.LivingRoom, DeviceKind.Fan, false, null),
            new("switch.dining_table_lights", "餐桌灯", SmartStack.DiningRoom, DeviceKind.Light, false, null),
            new("switch.kitchen_lights_right", "厨房灯", SmartStack.Kitchen, DeviceKind.Light, false, null),
            new("switch.balcony_lights", "阳台灯", SmartStack.Balcony, DeviceKind.Light, false, null),
            new("climate.daikinap59921", "客厅空调一", SmartStack.LivingRoom, DeviceKind.Aircon, false, null, 27.4),
            new("climate.daikinap16703", "客厅空调二", SmartStack.LivingRoom, DeviceKind.Aircon, false, null, 27.8),
            new("climate.daikinap79207", "主卧空调", "master", DeviceKind.Aircon, false, null, 26.9),
            new("climate.daikinap35095", "卧室二空调", "bedroom2", DeviceKind.Aircon, false, null, 28.1)
        ];

        return devices
            .Select(d => on.Contains(d.EntityId)
                ? d with
                {
                    On = true,
                    OnSince = now.AddMinutes(-95),
                    TargetTemperature = d.Kind == DeviceKind.Aircon ? 26 : null,
                    Mode = d.Kind == DeviceKind.Aircon ? "cool" : null
                }
                : d)
            .ToEquatableList();
    }

    private static EquatableList<BusInfo> Buses(DateTime now, int[] service74, int[] service54) =>
    [
        new BusInfo("74", "53139", service74.Select(m => new BusArrival(now.AddMinutes(m).AddSeconds(20), "SEA")).ToEquatableList()),
        new BusInfo("54", "53131", service54.Select(m => new BusArrival(now.AddMinutes(m).AddSeconds(20), "SDA")).ToEquatableList())
    ];
}
