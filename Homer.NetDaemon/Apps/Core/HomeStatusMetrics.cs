using System.Diagnostics.Metrics;
using Homer.NetDaemon.Entities;
using Homer.ServiceDefaults.Metrics;
using NetDaemon.AppModel;
using NetDaemon.HassModel.Entities;

namespace Homer.NetDaemon.Apps.Core;

/// <summary>
/// Pull-based gauges describing the state of the home (aircon, power, water heater). Values are read from
/// NetDaemon's cached entity state only when the exporter collects, so there is no per-event work and no
/// polling loop. Each app instance owns a Meter so a NetDaemon reload doesn't leave stale callbacks behind.
/// </summary>
[NetDaemonApp]
public sealed class HomeStatusMetrics : IDisposable
{
    private readonly Meter _meter = new(EntityMetrics.MeterName);

    private sealed record Aircon(
        ClimateEntity Climate,
        NumericSensorEntity Inside,
        NumericSensorEntity Outside,
        NumericSensorEntity Power,
        NumericSensorEntity Energy);

    public HomeStatusMetrics(
        ClimateEntities climate,
        SensorEntities sensors,
        SwitchEntities switches,
        InputNumberEntities inputNumbers)
    {
        Aircon[] aircons =
        [
            new(climate.Daikinap16703, sensors.Daikinap16703InsideTemperature, sensors.Daikinap16703OutsideTemperature,
                sensors.Daikinap16703CompressorEstimatedPowerConsumption, sensors.Daikinap16703EnergyConsumption),
            new(climate.Daikinap25067, sensors.Daikinap25067InsideTemperature, sensors.Daikinap25067OutsideTemperature,
                sensors.Daikinap25067CompressorEstimatedPowerConsumption, sensors.Daikinap25067EnergyConsumption),
            new(climate.Daikinap35095, sensors.Daikinap35095InsideTemperature, sensors.Daikinap35095OutsideTemperature,
                sensors.Daikinap35095CompressorEstimatedPowerConsumption, sensors.Daikinap35095EnergyConsumption),
            new(climate.Daikinap59921, sensors.Daikinap59921InsideTemperature, sensors.Daikinap59921OutsideTemperature,
                sensors.Daikinap59921CompressorEstimatedPowerConsumption, sensors.Daikinap59921EnergyConsumption),
            new(climate.Daikinap79207, sensors.Daikinap79207InsideTemperature, sensors.Daikinap79207OutsideTemperature,
                sensors.Daikinap79207CompressorEstimatedPowerConsumption, sensors.Daikinap79207EnergyConsumption),
            new(climate.Daikinap97235, sensors.Daikinap97235InsideTemperature, sensors.Daikinap97235OutsideTemperature,
                sensors.Daikinap97235CompressorEstimatedPowerConsumption, sensors.Daikinap97235EnergyConsumption),
        ];

        _meter.CreateObservableGauge("homer.aircon.active", () => aircons
                .Select(a => Measure(a.Climate.EntityId, a.Climate.State is null or "off" or "unavailable" ? 0 : 1)),
            "{aircon}", "1 when the aircon is running, 0 when off");

        _meter.CreateObservableGauge("homer.aircon.units_on", () => aircons
                .Count(a => a.Climate.State is not (null or "off" or "unavailable")),
            "{aircon}", "Number of aircons currently running");

        _meter.CreateObservableGauge("homer.aircon.temperature.indoor", () => Readings(aircons, a => a.Inside, a => a.Climate),
            "Cel", "Indoor temperature reported by each aircon");

        _meter.CreateObservableGauge("homer.aircon.temperature.outdoor", () => Readings(aircons, a => a.Outside, a => a.Climate),
            "Cel", "Outdoor unit temperature reported by each aircon");

        _meter.CreateObservableGauge("homer.aircon.compressor.power", () => Readings(aircons, a => a.Power, a => a.Climate, toWatts: true),
            "W", "Estimated compressor power draw of each aircon");

        // Lifetime energy totals from HA are monotonic, so export them as counters (use rate() / increase() for kWh per day).
        _meter.CreateObservableCounter("homer.aircon.energy", () => Readings(aircons, a => a.Energy, a => a.Climate),
            "kWh", "Cumulative energy used by each aircon");

        NumericSensorEntity[] plugPower =
        [
            sensors.Bedroom2IkeaPlugPower, sensors.Bedroom3IkeaPlugPower, sensors.Bedroom4IkeaPlugPower,
            sensors.LivingRoomIkeaPlugPower, sensors.MasterBedroomIkeaPlugPower, sensors.SmartWiFiPlugPower
        ];
        NumericSensorEntity[] plugEnergy =
        [
            sensors.Bedroom2IkeaPlugEnergy, sensors.Bedroom3IkeaPlugEnergy, sensors.Bedroom4IkeaPlugEnergy,
            sensors.LivingRoomIkeaPlugEnergy, sensors.MasterBedroomIkeaPlugEnergy, sensors.SmartWiFiPlugEnergy
        ];

        _meter.CreateObservableGauge("homer.plug.power", () => Readings(plugPower, toWatts: true),
            "W", "Current power draw of each smart plug");
        _meter.CreateObservableCounter("homer.plug.energy", () => Readings(plugEnergy),
            "kWh", "Cumulative energy used through each smart plug");

        var heater = switches.WaterHeaterSwitch;
        var minutesLeft = inputNumbers.WaterHeaterMinutesLeft;

        _meter.CreateObservableGauge("homer.water_heater.on", () => heater.IsOn() ? 1 : 0,
            "1", "1 while the water heater is heating");
        _meter.CreateObservableGauge("homer.water_heater.budget.remaining", () => minutesLeft.State ?? 0,
            "min", "Heating minutes left in today's water heater budget");
    }

    private static Measurement<double> Measure(string entityId, double value) =>
        new(value, new KeyValuePair<string, object?>("entity_id", entityId));

    private static IEnumerable<Measurement<double>> Readings(
        Aircon[] aircons, Func<Aircon, NumericSensorEntity> sensor, Func<Aircon, ClimateEntity> owner,
        bool toWatts = false)
    {
        foreach (var a in aircons)
        {
            if (Read(sensor(a), toWatts) is { } value)
            {
                yield return Measure(owner(a).EntityId, value);
            }
        }
    }

    private static IEnumerable<Measurement<double>> Readings(NumericSensorEntity[] sensors, bool toWatts = false)
    {
        foreach (var s in sensors)
        {
            if (Read(s, toWatts) is { } value)
            {
                yield return Measure(s.EntityId, value);
            }
        }
    }

    // Unavailable / unknown sensors yield no data point instead of a misleading 0.
    private static double? Read(NumericSensorEntity sensor, bool toWatts)
    {
        if (sensor.State is not { } value || double.IsNaN(value))
        {
            return null;
        }

        return toWatts && sensor.Attributes?.UnitOfMeasurement == "kW" ? value * 1000 : value;
    }

    public void Dispose() => _meter.Dispose();
}
