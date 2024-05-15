using System.Reactive.Linq;
using Homer.NetDaemon.Entities;
using NetDaemon.AppModel;
using Serilog;

namespace Homer.NetDaemon.Apps.Notify;

[NetDaemonApp]
public class Offline
{
    public Offline(
        NotifyServices notifyServices,
        SensorEntities sensorEntities,
        BinarySensorEntities binarySensorEntities,
        SwitchEntities switchEntities
    )
    {
        var count = 0;

        foreach (var entity in sensorEntities.EnumerateAll())
        {
            count++;
            entity.StateAllChanges()
                .Where(e => e.New?.State is null or "unavailable")
                .Subscribe(e =>
                {
                    notifyServices.MobileAppQinsIphone(
                        $"Sensor {entity.EntityId} is {e.New?.State}"
                    );
                });
        }

        foreach (var entity in binarySensorEntities.EnumerateAll())
        {
            count++;
            entity.StateAllChanges()
                .Where(e => e.New?.State is null or "unavailable")
                .Subscribe(e =>
                {
                    notifyServices.MobileAppQinsIphone(
                        $"Sensor {entity.EntityId} is {e.New?.State}"
                    );
                });
        }

        foreach (var entity in switchEntities.EnumerateAll())
        {
            count++;
            entity.StateAllChanges()
                .Where(e => e.New?.State is null or "unavailable")
                .Subscribe(e =>
                {
                    notifyServices.MobileAppQinsIphone(
                        $"Sensor {entity.EntityId} is {e.New?.State}"
                    );
                });
        }

        notifyServices.MobileAppQinsIphone(
            $"Registered {count} entities for offline detection."
        );
    }
}