using Homer.NetDaemon.Entities;
using NetDaemon.AppModel;
using NetDaemon.HassModel.Entities;
using System.Reactive.Linq;

namespace Homer.NetDaemon.Apps.Aircon;

[NetDaemonApp]
public class ClimateEnforcer
{
    private const int TargetTemperature = 26;
    private const string TargetFanMode = "auto";
    private const string TargetPresetMode = "eco";

    public ClimateEnforcer(ClimateEntities climateEntities)
    {
        foreach (var aircon in climateEntities.EnumerateAll())
        {
            aircon.StateChanges()
                .Where(s => s.Old.IsOff() && !s.New.IsOff())
                .Subscribe(_ =>
                {
                    aircon.SetTemperature(TargetTemperature);
                    aircon.SetFanMode(TargetFanMode);
                    aircon.SetPresetMode(TargetPresetMode);
                });
        }
    }
}