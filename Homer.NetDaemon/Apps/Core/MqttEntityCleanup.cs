using NetDaemon.AppModel;
using NetDaemon.Extensions.MqttEntityManager;

namespace Homer.NetDaemon.Apps.Core;

/// <summary>Removes MQTT entities that earlier versions of Homer created and no longer use.</summary>
[NetDaemonApp]
public class MqttEntityCleanup
{
    public MqttEntityCleanup(IMqttEntityManager manager)
    {
        manager.RemoveAsync("climate.water_heater_2");
        manager.RemoveAsync("climate.10");
        manager.RemoveAsync("binary_sensor.daikin");
        manager.RemoveAsync("button.daikin");
        manager.RemoveAsync("switch.daikin");
        manager.RemoveAsync("water_heater.daikin");
        manager.RemoveAsync("water_heater.daikin2");
        manager.RemoveAsync("water_heater.10");
        manager.RemoveAsync("sensor.water_heater");
        manager.RemoveAsync("sensor.10");
        manager.RemoveAsync("switch.10");
    }
}
