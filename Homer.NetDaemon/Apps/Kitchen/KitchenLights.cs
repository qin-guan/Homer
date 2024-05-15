using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Homer.NetDaemon.Entities;
using Homer.NetDaemon.Helpers;
using NetDaemon.AppModel;
using NetDaemon.Extensions.Scheduler;
using NetDaemon.HassModel.Entities;

namespace Homer.NetDaemon.Apps.Kitchen;

[NetDaemonApp]
public class KitchenLights
{
    private readonly SwitchEntities _switchEntities;
    
    private static readonly TimeOnly DawnTime = new(6, 20);
    private const string DawnTimeCron = "20 6 * * *";
    private static readonly TimeOnly NightTime = new(21, 45);
    private const string NightTimeCron = "45 21 * * *";

    public KitchenLights(
        ILogger<KitchenLights> logger,
        IScheduler scheduler,
        BinarySensorEntities binarySensorEntities,
        SwitchEntities switchEntities,
        SensorEntities sensors
    )
    {
        _switchEntities = switchEntities;

        var presenceEntities = new List<BinarySensorEntity>()
        {
            binarySensorEntities.PresenceSensorFp2B4c4PresenceSensor6,
            binarySensorEntities.KitchenTuyaPresencePresence
        };

        var presenceObservables = presenceEntities.Select(e => e.StateChanges()).Merge();

        if (presenceEntities.All(e => e.IsOff()))
        {
            TurnOffLights();
        }

        scheduler.ScheduleCron(DawnTimeCron, () =>
        {
            if (presenceEntities.Any(e => e.IsOn()))
            {
                TurnOnLights();
            }
        });

        scheduler.ScheduleCron(NightTimeCron, () =>
        {
            if (presenceEntities.Any(e => e.IsOn()))
            {
                TurnOnLights();
            }
        });

        presenceObservables
            .Where(e =>
            {
                logger.LogDebug("Kitchen presence state changed: {State}", e.New);
                return e.New.IsOn();
            })
            .Subscribe(_ => { TurnOnLights(); });

        presenceObservables
            .Where(e =>
            {
                logger.LogDebug("Kitchen presence state changed: {State}", e.New);
                return e.New.IsOff() && presenceEntities.All(entity => entity.IsOff());
            })
            .Subscribe(_ => { TurnOffLights(); });
    }

    private void TurnOffLights()
    {
        _switchEntities.KitchenLightsLeft.TurnOff();
        _switchEntities.KitchenLightsRight.TurnOff();
    }

    private void TurnOnLights()
    {
        if (TimeHelpers.TimeNow >= DawnTime &&
            TimeHelpers.TimeNow <= NightTime)
        {
            _switchEntities.KitchenLightsRight.TurnOn();
            _switchEntities.KitchenLightsLeft.TurnOff();
        }
        else
        {
            _switchEntities.KitchenLightsLeft.TurnOn();
            _switchEntities.KitchenLightsRight.TurnOff();
        }
    }
}
