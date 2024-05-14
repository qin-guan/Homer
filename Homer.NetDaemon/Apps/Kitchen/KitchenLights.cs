using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Homer.NetDaemon.Apps.LivingRoom;
using Homer.NetDaemon.Entities;
using NetDaemon.AppModel;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace Homer.NetDaemon.Apps.Kitchen;

[NetDaemonApp]
[Focus]
public class KitchenLights
{
    public KitchenLights(ILogger<KitchenLights> logger, IScheduler scheduler, BinarySensorEntities bse,
        SwitchEntities se, SensorEntities sensors)
    {
        bse.KitchenTuyaPresencePresence.StateChanges().Subscribe(e => logger.LogInformation("{0}", e));
        bse.KitchenTuyaPresencePresence.StateChanges()
            .WhenStateIsFor(
                e => e.IsOff(),
                TimeSpan.FromMinutes(1),
                scheduler
            )
            .Subscribe(_ =>
            {
                se.KitchenLightsLeft.TurnOff();
                se.KitchenLightsRight.TurnOff();
            });

        bse.KitchenTuyaPresencePresence.StateChanges()
            .Where(e => e.New.IsOn())
            .Subscribe(e =>
            {
                if (TimeOnly.FromDateTime(DateTime.Now) > new TimeOnly(22, 0)
                    && TimeOnly.FromDateTime(DateTime.Now) < new TimeOnly(6, 0)
                   )
                    se.KitchenLightsLeft.TurnOn();
                else
                {
                    if (sensors.PresenceSensorFp2B4c4LightSensorLightLevel.State < 60)
                    {
                        se.KitchenLightsRight.TurnOn();
                    }
                }
            });
    }
}