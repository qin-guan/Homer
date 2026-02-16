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
    private readonly InputBooleanEntity _manualOverride;
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
        _manualOverride = inputBooleanEntities.LiangYiMoShi;

        // Temperature-based fan speed control
        _disposables.Add(_temperatureSensor.StateChanges()
            .Where(_ => !_manualOverride.IsOn())
            .Throttle(TimeSpan.FromMinutes(2), scheduler)
            .Subscribe(e =>
            {
                eventsProcessedMeter.Add(1);
                AdjustFanSpeedBasedOnTemperature(e.New?.State);
            }));

        // Weather forecast-based adjustments
        _disposables.Add(apiFactory.CreateForecast()
            .Select(f => f.Data.Items.First().Forecasts.First(f => f.Area == "Bishan").Value)
            .DistinctUntilChanged()
            .Where(_ => !_manualOverride.IsOn())
            .Subscribe(forecast =>
            {
                eventsProcessedMeter.Add(1);
                AdjustFanBasedOnWeather(forecast);
            }));

        // Light sensor-based brightness and color temperature control
        _disposables.Add(_lightSensor.StateChanges()
            .Where(_ => !_manualOverride.IsOn())
            .Throttle(TimeSpan.FromMinutes(3), scheduler)
            .Subscribe(e =>
            {
                eventsProcessedMeter.Add(1);
                AdjustLightBasedOnAmbient(e.New?.State);
            }));

        // Sun position-based color temperature adjustments
        _disposables.Add(Observable.Interval(TimeSpan.FromMinutes(30), scheduler)
            .Where(_ => !_manualOverride.IsOn())
            .Subscribe(_ =>
            {
                eventsProcessedMeter.Add(1);
                AdjustColorTemperatureBasedOnTimeOfDay();
            }));
    }

    private void AdjustFanSpeedBasedOnTemperature(double? temperature)
    {
        if (!temperature.HasValue || !_fan.IsOn())
            return;

        var temp = temperature.Value;
        int targetPercentage;

        if (temp >= TemperatureVeryHot)
        {
            targetPercentage = 100; // Maximum speed
            _logger.LogInformation("Very hot ({Temp}°C), setting fan to 100%", temp);
        }
        else if (temp >= TemperatureHot)
        {
            targetPercentage = 75;
            _logger.LogInformation("Hot ({Temp}°C), setting fan to 75%", temp);
        }
        else if (temp >= TemperatureWarm)
        {
            targetPercentage = 50;
            _logger.LogInformation("Warm ({Temp}°C), setting fan to 50%", temp);
        }
        else if (temp >= TemperatureCool)
        {
            targetPercentage = 25;
            _logger.LogInformation("Cool ({Temp}°C), setting fan to 25%", temp);
        }
        else
        {
            // Too cold, turn off fan
            _logger.LogInformation("Cold ({Temp}°C), turning fan off", temp);
            _fan.TurnOff();
            return;
        }

        _fan.SetPercentage(targetPercentage);
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

        if (isRainy)
        {
            // Reduce fan speed during rainy weather as it's typically cooler
            _logger.LogInformation("Rainy weather detected ({Forecast}), reducing fan speed slightly", forecast);
            _fan.DecreaseSpeed(percentageStep: 10);
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

        var currentTime = TimeOnly.FromDateTime(DateTime.Now);
        var isDay = _sun.State == "above_horizon";
        var temperature = _temperatureSensor.State;

        int targetColorTemp;

        // Evening (after 6 PM) - warm light
        if (currentTime.Hour >= 18 || currentTime.Hour < 6)
        {
            targetColorTemp = ColorTempWarm;
            _logger.LogInformation("Evening/Night time, setting warm color temperature ({ColorTemp}K)", targetColorTemp);
        }
        // Hot weather - cool light to create psychological cooling effect
        else if (temperature >= TemperatureHot)
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
        if (!_manualOverride.IsOn())
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
