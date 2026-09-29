using System.Diagnostics.Metrics;
using Homer.NetDaemon.Entities;
using Homer.NetDaemon.Helpers;
using Homer.ServiceDefaults.Metrics;
using NetDaemon.AppModel;

namespace Homer.NetDaemon.Apps.Aircon;

/// <summary>
/// Pull-based gauges for each aircon (running state, temperatures, power, energy). Values are read from NetDaemon's
/// cached entity state only when the exporter collects, so there is no per-event work and no polling loop. Each app
/// instance owns a Meter so a NetDaemon reload doesn't leave stale callbacks behind.
/// </summary>
[NetDaemonApp]
public sealed class AirconMetrics : IDisposable
{
    private readonly Meter _meter = new(EntityMetrics.MeterName);

    private sealed record AirconSensors(
        ClimateEntity Climate,
        NumericSensorEntity Inside,
        NumericSensorEntity Outside,
        NumericSensorEntity Power,
        NumericSensorEntity Energy);

    public AirconMetrics(ClimateEntities climate, SensorEntities sensors)
    {
        AirconSensors[] aircons =
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
                .Select(a => SensorMeasurements.Measure(a.Climate.EntityId,
                    a.Climate.State is null or "off" or "unavailable" ? 0 : 1)),
            "{aircon}", "1 when the aircon is running, 0 when off");

        _meter.CreateObservableGauge("homer.aircon.units_on", () => aircons
                .Count(a => a.Climate.State is not (null or "off" or "unavailable")),
            "{aircon}", "Number of aircons currently running");

        _meter.CreateObservableGauge("homer.aircon.temperature.indoor", () => Readings(aircons, a => a.Inside),
            "Cel", "Indoor temperature reported by each aircon");

        _meter.CreateObservableGauge("homer.aircon.temperature.outdoor", () => Readings(aircons, a => a.Outside),
            "Cel", "Outdoor unit temperature reported by each aircon");

        _meter.CreateObservableGauge("homer.aircon.compressor.power", () => Readings(aircons, a => a.Power, toWatts: true),
            "W", "Estimated compressor power draw of each aircon");

        // Lifetime energy totals from HA are monotonic, so export them as counters (use rate() / increase() for kWh per day).
        _meter.CreateObservableCounter("homer.aircon.energy", () => Readings(aircons, a => a.Energy),
            "kWh", "Cumulative energy used by each aircon");
    }

    // Tagged by the aircon's climate entity rather than the sensor, so every aircon series lines up.
    private static IEnumerable<Measurement<double>> Readings(
        AirconSensors[] aircons, Func<AirconSensors, NumericSensorEntity> sensor, bool toWatts = false)
    {
        foreach (var a in aircons)
        {
            if (SensorMeasurements.Read(sensor(a), toWatts) is { } value)
            {
                yield return SensorMeasurements.Measure(a.Climate.EntityId, value);
            }
        }
    }

    public void Dispose() => _meter.Dispose();
}
