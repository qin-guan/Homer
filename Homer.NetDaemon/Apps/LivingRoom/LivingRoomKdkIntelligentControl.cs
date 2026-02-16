using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Homer.NetDaemon.Entities;
using Homer.NetDaemon.Services;
using Homer.ServiceDefaults.Metrics;
using NetDaemon.AppModel;
using NetDaemon.HassModel.Entities;

namespace Homer.NetDaemon.Apps.LivingRoom;

/// <summary>
/// Intelligently controls the living room KDK fan speed, brightness, and color temperature
/// based on weather data from Singapore public APIs and lighting conditions.
/// </summary>
[NetDaemonApp]
public class LivingRoomKdkIntelligentControl : IAsyncInitializable, IAsyncDisposable
{
    private readonly ILogger<LivingRoomKdkIntelligentControl> _logger;
    private readonly FanEntity _fan;
    private readonly LightEntity _light;
    private readonly NumericSensorEntity _temperatureSensor;
    private readonly NumericSensorEntity _lightSensor;
    private readonly SunEntity _sun;
    private readonly InputBooleanEntity _laundryModeSwitch;  // Used as manual override
    private readonly IScheduler _scheduler;
    private readonly List<IDisposable> _disposables = [];

    // Weather-based fan speed thresholds
    private const double TemperatureVeryHot = 30.0;
    private const double TemperatureHot = 28.0;
    private const double TemperatureWarm = 26.0;
    private const double TemperatureCool = 24.0;

    // Light-based brightness thresholds
    private const double LightLevelBright = 50.0;
    private const double LightLevelNormal = 20.0;
    private const double LightLevelDim = 10.0;

    // Color temperature settings (in Kelvin)
    private const int ColorTempWarm = 2700;  // Warm white for evening
    private const int ColorTempNeutral = 4000;  // Neutral for day
    private const int ColorTempCool = 5500;  // Cool white for hot weather

    // Time of day thresholds
    private const int EveningStartHour = 18;  // 6 PM
    private const int MorningStartHour = 6;   // 6 AM

    // Minimum fan speed to maintain during rainy weather
    private const int MinimumFanSpeedPercent = 25;

    public LivingRoomKdkIntelligentControl(
        ILogger<LivingRoomKdkIntelligentControl> logger,
        IScheduler scheduler,
        ApiObservableFactoryService apiFactory,
        FanEntities fanEntities,
        LightEntities lightEntities,
        SensorEntities sensorEntities,
        SunEntities sunEntities,
        InputBooleanEntities inputBooleanEntities
    )
    {
        var eventsProcessedMeter = EntityMetrics.MeterInstance.CreateCounter<int>(
            "homer.netdaemon.living_room_kdk_intelligent.events_processed"
        );

        _logger = logger;
        _fan = fanEntities.LivingRoomKdk;
        _light = lightEntities.LivingRoomKdk;
        _temperatureSensor = sensorEntities.Daikinap16703InsideTemperature;
        _lightSensor = sensorEntities.PresenceSensorFp2B4c4LightSensorLightLevel;
        _sun = sunEntities.Sun;
        _laundryModeSwitch = inputBooleanEntities.LiangYiMoShi;
        _scheduler = scheduler;

        // Temperature-based fan speed control
        _disposables.Add(_temperatureSensor.StateChanges()
            .Where(_ => !_laundryModeSwitch.IsOn())
            .Throttle(TimeSpan.FromMinutes(2), scheduler)
            .Subscribe(e =>
            {
                eventsProcessedMeter.Add(1);
                AdjustFanSpeedBasedOnTemperature(e.New?.State);
            }));

        // Weather forecast-based adjustments
        _disposables.Add(apiFactory.CreateForecast()
            .Select(f =>
            {
                var item = f.Data.Items.FirstOrDefault();
                if (item == null) return null;
                var forecast = item.Forecasts.FirstOrDefault(fc => fc.Area == "Bishan");
                return forecast?.Value;
            })
            .Where(forecast => forecast != null)
            .DistinctUntilChanged()
            .Where(_ => !_laundryModeSwitch.IsOn())
            .Subscribe(forecast =>
            {
                eventsProcessedMeter.Add(1);
                AdjustFanBasedOnWeather(forecast!);
            }));

        // Light sensor-based brightness and color temperature control
        _disposables.Add(_lightSensor.StateChanges()
            .Where(_ => !_laundryModeSwitch.IsOn())
            .Throttle(TimeSpan.FromMinutes(3), scheduler)
            .Subscribe(e =>
            {
                eventsProcessedMeter.Add(1);
                AdjustLightBasedOnAmbient(e.New?.State);
            }));

        // Sun position-based color temperature adjustments
        _disposables.Add(Observable.Interval(TimeSpan.FromMinutes(30), scheduler)
            .Where(_ => !_laundryModeSwitch.IsOn())
            .Subscribe(_ =>
            {
                eventsProcessedMeter.Add(1);
                AdjustColorTemperatureBasedOnTimeOfDay();
            }));
    }

    private void AdjustFanSpeedBasedOnTemperature(double? temperature)
    {
        if (!temperature.HasValue)
            return;

        var temp = temperature.Value;
        int targetPercentage;

        if (temp >= TemperatureVeryHot)
        {
            targetPercentage = 100; // Maximum speed
            _logger.LogInformation("Very hot ({Temp}°C), setting fan to 100%", temp);
            if (!_fan.IsOn()) _fan.TurnOn();
            _fan.SetPercentage(targetPercentage);
        }
        else if (temp >= TemperatureHot)
        {
            targetPercentage = 75;
            _logger.LogInformation("Hot ({Temp}°C), setting fan to 75%", temp);
            if (!_fan.IsOn()) _fan.TurnOn();
            _fan.SetPercentage(targetPercentage);
        }
        else if (temp >= TemperatureWarm)
        {
            targetPercentage = 50;
            _logger.LogInformation("Warm ({Temp}°C), setting fan to 50%", temp);
            if (!_fan.IsOn()) _fan.TurnOn();
            _fan.SetPercentage(targetPercentage);
        }
        else if (temp >= TemperatureCool)
        {
            targetPercentage = 25;
            _logger.LogInformation("Cool ({Temp}°C), setting fan to 25%", temp);
            if (!_fan.IsOn()) _fan.TurnOn();
            _fan.SetPercentage(targetPercentage);
        }
        else
        {
            // Too cold, turn off fan
            _logger.LogInformation("Cold ({Temp}°C), turning fan off", temp);
            _fan.TurnOff();
            return;
        }
    }

    private void AdjustFanBasedOnWeather(string forecast)
    {
        if (!_fan.IsOn())
            return;

        var isRainy = forecast is (
            "Moderate Rain" or
            "Heavy Rain" or
            "Passing Showers" or
            "Light Showers" or
            "Showers" or
            "Heavy Showers" or
            "Thundery Showers" or
            "Heavy Thundery Showers" or
            "Heavy Thundery Showers with Gusty Winds"
        );

        if (isRainy && _temperatureSensor.State.HasValue)
        {
            // During rainy weather, set fan to a comfortable lower speed based on temperature
            // Rain typically cools the environment, so we reduce speed but maintain minimum ventilation
            var temperature = _temperatureSensor.State.Value;
            int targetPercentage;

            if (temperature >= TemperatureHot)
            {
                targetPercentage = 50;  // Still hot despite rain
            }
            else if (temperature >= TemperatureWarm)
            {
                targetPercentage = 35;  // Moderate speed
            }
            else
            {
                targetPercentage = MinimumFanSpeedPercent;  // Minimum ventilation
            }

            _logger.LogInformation(
                "Rainy weather detected ({Forecast}), adjusting fan speed to {Percentage}% based on temperature {Temp}°C",
                forecast, targetPercentage, temperature);
            _fan.SetPercentage(targetPercentage);
        }
    }

    private void AdjustLightBasedOnAmbient(double? ambientLight)
    {
        if (!ambientLight.HasValue || !_light.IsOn())
            return;

        var light = ambientLight.Value;
        int targetBrightness;

        if (light >= LightLevelBright)
        {
            // Very bright, minimal fan light needed
            targetBrightness = 20;
            _logger.LogInformation("Bright ambient light ({Light}), setting fan light to 20%", light);
        }
        else if (light >= LightLevelNormal)
        {
            // Normal light levels
            targetBrightness = 50;
            _logger.LogInformation("Normal ambient light ({Light}), setting fan light to 50%", light);
        }
        else if (light >= LightLevelDim)
        {
            // Dim, increase fan light
            targetBrightness = 75;
            _logger.LogInformation("Dim ambient light ({Light}), setting fan light to 75%", light);
        }
        else
        {
            // Dark, maximum fan light
            targetBrightness = 100;
            _logger.LogInformation("Dark ambient light ({Light}), setting fan light to 100%", light);
        }

        _light.TurnOn(new LightTurnOnParameters { BrightnessPct = targetBrightness });
    }

    private void AdjustColorTemperatureBasedOnTimeOfDay()
    {
        if (!_light.IsOn())
            return;

        // Using LocalDateTime to match the local timezone of the smart home system (Singapore Time - UTC+8)
        var currentTime = TimeOnly.FromDateTime(_scheduler.Now.LocalDateTime);
        var isDay = _sun.State == "above_horizon";
        var temperature = _temperatureSensor.State;

        int targetColorTemp;

        // Evening (after 6 PM) or night (before 6 AM) - warm light
        if (currentTime.Hour >= EveningStartHour || currentTime.Hour < MorningStartHour)
        {
            targetColorTemp = ColorTempWarm;
            _logger.LogInformation("Evening/Night time, setting warm color temperature ({ColorTemp}K)", targetColorTemp);
        }
        // Hot weather - cool light to create psychological cooling effect
        else if (temperature.HasValue && temperature >= TemperatureHot)
        {
            targetColorTemp = ColorTempCool;
            _logger.LogInformation("Hot weather, setting cool color temperature ({ColorTemp}K)", targetColorTemp);
        }
        // Daytime - neutral light
        else if (isDay)
        {
            targetColorTemp = ColorTempNeutral;
            _logger.LogInformation("Daytime, setting neutral color temperature ({ColorTemp}K)", targetColorTemp);
        }
        else
        {
            targetColorTemp = ColorTempWarm;
            _logger.LogInformation("Default to warm color temperature ({ColorTemp}K)", targetColorTemp);
        }

        _light.TurnOn(new LightTurnOnParameters { ColorTempKelvin = targetColorTemp });
    }

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Living Room KDK Intelligent Control initialized");

        // Perform initial adjustments
        if (!_laundryModeSwitch.IsOn())
        {
            AdjustFanSpeedBasedOnTemperature(_temperatureSensor.State);
            AdjustLightBasedOnAmbient(_lightSensor.State);
            AdjustColorTemperatureBasedOnTimeOfDay();
        }

        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _disposables.ForEach(d => d.Dispose());
        return ValueTask.CompletedTask;
    }
}
