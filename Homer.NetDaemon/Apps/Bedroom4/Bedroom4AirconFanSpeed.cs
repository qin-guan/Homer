using System.Reactive.Linq;
using Homer.NetDaemon.Entities;
using NetDaemon.AppModel;
using NetDaemon.Extensions.Scheduler;
using NetDaemon.HassModel.Entities;

namespace Homer.NetDaemon.Apps.Bedroom4;

/// <summary>
/// Boosts the Bedroom 4 fan briefly when the aircon starts, and raises it again a while after the aircon stops.
/// </summary>
[NetDaemonApp]
public class Bedroom4AirconFanSpeed
{
    public Bedroom4AirconFanSpeed(
        INetDaemonScheduler netDaemonScheduler,
        InputBooleanEntities inputBooleanEntities,
        InputNumberEntities inputNumberEntities,
        ClimateEntities climateEntities
    )
    {
        climateEntities.Daikinap97235.StateChanges()
            .Where(s => s.Old.IsOff())
            .Subscribe(_ =>
            {
                if (inputBooleanEntities.Bedroom4Presence.IsOff()) return;

                inputNumberEntities.Bedroom4FanSpeed.SetValue(16 * 5);

                netDaemonScheduler.RunIn(TimeSpan.FromMinutes(5),
                    () => { inputNumberEntities.Bedroom4FanSpeed.SetValue(16); });
            });

        climateEntities.Daikinap97235.StateChanges()
            .Where(s => s.New.IsOff())
            .Subscribe(_ =>
            {
                if (inputBooleanEntities.Bedroom4Presence.IsOff()) return;

                netDaemonScheduler.RunIn(TimeSpan.FromHours(2),
                    () =>
                    {
                        inputNumberEntities.Bedroom4FanSpeed.SetValue(
                            16 * 2
                        );
                    });
            });
    }
}
