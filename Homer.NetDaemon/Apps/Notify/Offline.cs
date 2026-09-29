using System.Collections.Frozen;
using System.Reactive.Concurrency;
using Homer.NetDaemon.Entities;
using NetDaemon.AppModel;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace Homer.NetDaemon.Apps.Notify;

// [Focus]
[NetDaemonApp]
public class Offline
{
    private static readonly TimeSpan OfflineDelay = TimeSpan.FromMinutes(1);

    private readonly NotifyServices _notifyServices;
    private readonly IScheduler _scheduler;
    private readonly object _gate = new();

    private class TrackedDevice(string? name, IReadOnlyList<Entity> entities)
    {
        public string? Name { get; } = name;
        public IReadOnlyList<Entity> Entities { get; } = entities;
        public bool IsOffline => Entities.All(e => e.IsOffline());
        public bool ReportedOffline { get; set; }
        public IDisposable? PendingOfflineReport { get; set; }
    }

    public Offline(
        NotifyServices notifyServices,
        IHaContext haContext,
        IScheduler scheduler
    )
    {
        _notifyServices = notifyServices;
        _scheduler = scheduler;

        var devices = haContext.GetAllEntities()
            .Where(e => e.Registration?.Device?.Id is not null)
            .ExcludePrinter()
            .GroupBy(e => e.Registration!.Device!.Id)
            .Select(g => new TrackedDevice(g.First().Registration!.Device!.Name, g.ToList()))
            .ToList();

        var devicesByEntityId = devices
            .SelectMany(d => d.Entities, (device, entity) => (entity.EntityId, device))
            .ToFrozenDictionary(x => x.EntityId, x => x.device);

        // NetDaemon parses every state change once per subscription, so a subscription per entity
        // gets expensive on a busy Home Assistant. Route state changes to devices through one instead.
        haContext.StateAllChanges()
            .Subscribe(e =>
            {
                if (devicesByEntityId.TryGetValue(e.Entity.EntityId, out var device))
                {
                    OnDeviceStateChanged(device);
                }
            });

        notifyServices.MobileAppQinSS26Ultra(
            $"Registered {devices.Count} devices for offline detection."
        );
    }

    private void OnDeviceStateChanged(TrackedDevice device)
    {
        lock (_gate)
        {
            if (!device.IsOffline)
            {
                device.PendingOfflineReport?.Dispose();
                device.PendingOfflineReport = null;

                if (device.ReportedOffline)
                {
                    device.ReportedOffline = false;
                    _notifyServices.MobileAppQinSS26Ultra($"Device {device.Name} is back online");
                }

                return;
            }

            if (device.ReportedOffline || device.PendingOfflineReport is not null)
            {
                return;
            }

            // A device is only reported once all of its entities have stayed offline for OfflineDelay.
            IDisposable? report = null;
            report = _scheduler.Schedule(OfflineDelay, () =>
            {
                lock (_gate)
                {
                    if (device.PendingOfflineReport != report)
                    {
                        return;
                    }

                    device.PendingOfflineReport = null;

                    if (!device.IsOffline)
                    {
                        return;
                    }

                    device.ReportedOffline = true;
                    _notifyServices.MobileAppQinSS26Ultra($"Device {device.Name} is offline");
                }
            });
            device.PendingOfflineReport = report;
        }
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
