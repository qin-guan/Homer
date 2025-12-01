using System.Reactive.Linq;
using Homer.NetDaemon.Entities;
using NetDaemon.AppModel;
using NetDaemon.HassModel.Entities;

namespace Homer.NetDaemon.Apps.Aircon;

[NetDaemonApp]
public class ClimateEnforcer
{
    public ClimateEnforcer(ClimateEntities climateEntities)
    {
        foreach (var aircon in climateEntities.EnumerateAll())
        {
            aircon.StateChanges()
                .Where(s => s.Old.IsOff())
                .Subscribe(_ =>
                {
                    aircon.SetTemperature(27);
                });
        }
    }
}