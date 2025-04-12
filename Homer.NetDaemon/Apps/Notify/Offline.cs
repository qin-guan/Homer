using System.Collections.Immutable;
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
        IHaContext haContext
    )
    {
        var devices = haContext.GetAllEntities()
            .Where(e => e.Registration?.Device?.Id is not null)
            .ExcludePrinter()
            .GroupBy(e => e.Registration?.Device?.Id).ToImmutableArray();

        foreach (var item in devices)
        {
            var id = item.Key;

            item.StateAllChanges()
                .Where(_ => item.All(e2 => e2.IsOffline()))
                .Subscribe(_ =>
                {
                    notifyServices.MobileAppQinsIphone(
                        $"Device {id} is offline"
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
        return entity.State is "unknown" or "unavailable" or null;
    }
}