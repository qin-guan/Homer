using System.Reactive.Concurrency;
using Homer.NetDaemon.Entities;
using NetDaemon.AppModel;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

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

        foreach (var entity in sensorEntities.EnumerateAll().FilterPrinter())
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

        foreach (var entity in binarySensorEntities.EnumerateAll().FilterPrinter())
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

        foreach (var entity in switchEntities.EnumerateAll().FilterPrinter())
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

public static class OfflineExtensions
{
    public static IEnumerable<T> FilterPrinter<T>(this IEnumerable<T> entities) where T : IEntityCore
    {
        return entities.Where(e => !e.EntityId.Contains("brother_dcp"));
    }
}