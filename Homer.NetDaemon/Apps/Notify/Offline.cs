using System.Reactive.Concurrency;
using System.Reactive.Linq;
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
        IScheduler scheduler,
        IHaRegistry registry
    )
    {
        foreach (var device in registry.Devices)
        {
            device.Entities.Select(e => e.StateChanges()).Merge().DistinctUntilChanged()
                .Where(e => e.New?.State is null or "unavailable")
                .Subscribe(e =>
                {
                    notifyServices.MobileAppQinsIphone(
                        $"Device {device.Name} is offline"
                    );
                });
        }

        notifyServices.MobileAppQinsIphone(
            $"Registered {registry.Devices.Count} devices for offline detection."
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