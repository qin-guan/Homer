using System.Reactive.Concurrency;
using Homer.NetDaemon.Entities;
using NetDaemon.AppModel;
using NetDaemon.HassModel;

namespace Homer.NetDaemon.Apps.Notify;

[NetDaemonApp]
public class Offline
{
    public Offline(
        NotifyServices notifyServices,
        SensorEntities sensorEntities,
        BinarySensorEntities binarySensorEntities,
        SwitchEntities switchEntities,
        IScheduler scheduler
    )
    {
        var count = 0;

        foreach (var entity in sensorEntities.EnumerateAll())
        {
            var isPrinter = entity.Attributes?.Name?.Contains("dcp_") ?? false;
            if (isPrinter)
            {
                continue;
            }

            count++;
            entity.StateAllChanges()
                .WhenStateIsFor(e => e?.State is null or "unavailable", TimeSpan.FromMinutes(1), scheduler)
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
                .WhenStateIsFor(e => e?.State is null or "unavailable", TimeSpan.FromMinutes(1), scheduler)
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
                .WhenStateIsFor(e => e?.State is null or "unavailable", TimeSpan.FromMinutes(1), scheduler)
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