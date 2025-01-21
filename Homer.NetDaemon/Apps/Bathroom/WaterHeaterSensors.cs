using System.Reactive.Concurrency;
using Homer.NetDaemon.Apps.Daikin;
using NetDaemon.AppModel;
using NetDaemon.Extensions.MqttEntityManager;

namespace Homer.NetDaemon.Apps.Bathroom;

// [Focus]
[NetDaemonApp]
public class WaterHeaterSensors(IDaikinApi daikinApi, IMqttEntityManager entityManager, IScheduler scheduler)
    : IAsyncInitializable
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        scheduler.SchedulePeriodic(TimeSpan.FromSeconds(10), () =>
        {
            Task.Run(async () =>
            {
                var devices = await daikinApi.GetDevicesAsync();

                foreach (var device in devices.Response.Devices)
                {
                    var sensorId = $"sensor.water_heater_{device.Id}_current_temperature";
                    var actuatorId = $"switch.water_heater_{device.Id}_actuator";

                    await entityManager.RemoveAsync(sensorId);
                    await entityManager.CreateAsync(sensorId, new EntityCreationOptions
                    {
                        Name = $"Water Heater {device.Id} Temperature",
                        DeviceClass = "temperature"
                    }, new
                    {
                        unit_of_measurement = "\u00b0C",
                    });
                    await entityManager.SetStateAsync(sensorId, device.Data.Temperature.ToString());

                    await entityManager.CreateAsync(actuatorId, new EntityCreationOptions
                    {
                        Name = $"Water Heater {device.Id} Actuator"
                    });
                    await entityManager.SetStateAsync(actuatorId, "ON");
                }
            }, cancellationToken);
        });
    }
}