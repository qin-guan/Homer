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
                    await entityManager.RemoveAsync(actuatorId);
                }
            }, cancellationToken);
        });
    }
}