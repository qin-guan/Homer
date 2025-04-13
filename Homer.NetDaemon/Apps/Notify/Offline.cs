using System.Collections.Immutable;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Homer.NetDaemon.Entities;
using NetDaemon.AppModel;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace Homer.NetDaemon.Apps.Notify;

[Focus]
[NetDaemonApp]
public class Offline
{
    public Offline(
        NotifyServices notifyServices,
        IHaContext haContext,
        IScheduler scheduler
    )
    {
        var offlineDevices = new List<string>();

        var devices = haContext.GetAllEntities()
            .Where(e => e.Registration?.Device?.Id is not null)
            .ExcludePrinter()
            .GroupBy(e => e.Registration?.Device?.Id).ToImmutableArray();

        foreach (var item in devices)
        {
            var id = item.Key!;

            item.StateAllChanges()
                .Where(_ => !offlineDevices.Contains(id))
                .WhenStateIsFor(_ => item.All(e2 => e2.IsOffline()), TimeSpan.FromMinutes(1), scheduler)
                .Subscribe(e =>
                {
                    offlineDevices.Add(id);
                    notifyServices.MobileAppQinsIphone(
                        $"Device {e.Entity.Registration?.Device?.Name} is offline"
                    );
                });

            item.StateAllChanges()
                .Where(_ => offlineDevices.Contains(id))
                .Where(_ => item.All(e2 => !e2.IsOffline()))
                .Subscribe(e =>
                {
                    offlineDevices.Remove(id);
                    notifyServices.MobileAppQinsIphone(
                        $"Device {e.Entity.Registration?.Device?.Name} is back online"
                    );
                });
        }

        notifyServices.MobileAppQinsIphone(
            $"Registered {devices.Length} devices for offline detection."
        );
    }
}

public static class OfflineExtensions
{
    public static IEnumerable<T> ExcludePrinter<T>(this IEnumerable<T> entities) where T : IEntityCore
    {
        return entities.Where(e => !e.EntityId.Contains("brother_dcp"));
    }

    public static bool IsOffline<T>(this T entity) where T : Entity
    {
        return entity.State?.ToLower() is "unknown" or "unavailable" or null;
    }
}