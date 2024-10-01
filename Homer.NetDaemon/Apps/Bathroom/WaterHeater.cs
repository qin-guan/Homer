using NetDaemon.AppModel;
using NetDaemon.Extensions.MqttEntityManager;

namespace Homer.NetDaemon.Apps.Bathroom;

[Focus]
[NetDaemonApp]
public class WaterHeater(IMqttEntityManager entityManager, IDaikinApi daikinApi) : IAsyncInitializable, IAsyncDisposable
{
    private readonly List<string> _entityIds = [];

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var devices = await daikinApi.GetDevices();

        var response = new DaikinApiGetDevicesResponseResponseDevice[]
        {
            new DaikinApiGetDevicesResponseResponseDevice(
                10,
                "Test",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                false,
                1,
                "",
                false,
                null,
                true,
                10,
                "",
                true,
                null,
                new DaikinApiGetDevicesResponseResponseDeviceData(
                    1,
                    "",
                    null,
                    [],
                    "",
                    0,
                    0,
                    0,
                    "",
                    false,
                    0,
                    "",
                    0,
                    false,
                    "",
                    0
                )
            )
        };

        foreach (var device in response)
        {
            var heaterEntityId = $"switch.{device.Id}";
            _entityIds.Add(heaterEntityId);
            await entityManager.CreateAsync(heaterEntityId);
            await entityManager.SetStateAsync(heaterEntityId, "off");
            
            var sensorEntityId = $"sensor.{device.Id}";
            _entityIds.Add(sensorEntityId);
            await entityManager.CreateAsync(sensorEntityId, new EntityCreationOptions
            {
                DeviceClass = "humidity"
            });
            await entityManager.SetStateAsync(sensorEntityId, "11");
            
            var waterHeaterEntityId = $"climate.{device.Id}";
            _entityIds.Add(waterHeaterEntityId);

            await entityManager.CreateAsync(waterHeaterEntityId, new EntityCreationOptions
            {
                Name = $"{device.Name} Water Heater",
            }, new
            {
                temperature = 22.0,
                hvac_mode = "heat",
                min_temp = 16.0,
                max_temp = 30.0
            });

            // await entityManager.SetAttributesAsync(waterHeaterEntityId, new
            // {
            //     heater = heaterEntityId,
            //     target_sensor = sensorEntityId,
            //     min_temp = 45,
            //     max_temp = 60,
            //     ac_mode = false,
            //     target_temp = 50,
            //     initial_hvac_mode = "off",
            // });

            (await entityManager.PrepareCommandSubscriptionAsync(waterHeaterEntityId)).Subscribe(Console.WriteLine);
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var id in _entityIds)
        {
            await entityManager.RemoveAsync(id);
        }
    }
}