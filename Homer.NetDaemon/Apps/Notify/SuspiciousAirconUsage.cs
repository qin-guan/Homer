using System.Reactive.Linq;
using Homer.NetDaemon.Entities;
using NetDaemon.AppModel;
using NetDaemon.HassModel.Entities;

namespace Homer.NetDaemon.Apps.Notify;

[NetDaemonApp]
public class SuspiciousAirconUsage
{
    public SuspiciousAirconUsage(ClimateEntities climateEntities, NotifyServices notifyServices)
    {
        var aircons = climateEntities.EnumerateAll().ToList();

        aircons.Select(e => e.StateChanges()).Merge()
            .Where(e => e.New.IsOn())
            .Subscribe(_ =>
            {
                if (!aircons.All(e => e.IsOn())) return;
                notifyServices.MobileAppQinsIphone("All aircons are on! Is this a mistake?", "Check aircon usage!");
            });
    }
}