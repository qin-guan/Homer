using System.Diagnostics.Metrics;
using Homer.NetDaemon.Entities;
using Homer.NetDaemon.Helpers;
using Homer.ServiceDefaults.Metrics;
using NetDaemon.AppModel;

namespace Homer.NetDaemon.Apps.Core;

/// <summary>
/// Pull-based power and energy readings for each smart plug, read from NetDaemon's cached entity state only when the
/// exporter collects. Each app instance owns a Meter so a NetDaemon reload doesn't leave stale callbacks behind.
/// </summary>
[NetDaemonApp]
public sealed class SmartPlugMetrics : IDisposable
{
    private readonly Meter _meter = new(EntityMetrics.MeterName);

    public SmartPlugMetrics(SensorEntities sensors)
    {
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

        _meter.CreateObservableGauge("homer.plug.power", () => SensorMeasurements.Readings(plugPower, toWatts: true),
            "W", "Current power draw of each smart plug");
        _meter.CreateObservableCounter("homer.plug.energy", () => SensorMeasurements.Readings(plugEnergy),
            "kWh", "Cumulative energy used through each smart plug");
    }

    public void Dispose() => _meter.Dispose();
}
