using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Homer.NetDaemon.Apps.Core;
using Homer.NetDaemon.Entities;
using NetDaemon.AppModel;
using NetDaemon.Extensions.Scheduler;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace Homer.NetDaemon.Apps.Bedroom4;

[NetDaemonApp]
public class Bedroom4Lights : Occupancy
{
    private readonly SensorEntities _sensorEntities;
    public bool TooBright => _sensorEntities.PresenceSensorFp2B4c4LightSensorLightLevel.State > 30;

    public Bedroom4Lights(
        IScheduler scheduler,
        INetDaemonScheduler netDaemonScheduler,
        InputDatetimeEntities inputDatetimeEntities,
        InputBooleanEntities inputBooleanEntities,
        BinarySensorEntities binarySensorEntities,
        SensorEntities sensorEntities,
        SwitchEntities switchEntities,
        RemoteEntities remoteEntities,
        ClimateEntities climateEntities,
        FanEntities fanEntities
    ) : base(
        inputDatetimeEntities.Bedroom4LastPresence,
        inputBooleanEntities.Bedroom4Presence,
        [binarySensorEntities.Bedroom4DoorContact],
        [binarySensorEntities.ScreekHumanSensor2a06ead0Zone1Presence],
        TimeSpan.FromSeconds(5)
    )
    {
        _sensorEntities = sensorEntities;

        inputBooleanEntities.Bedroom4Presence.StateChanges()
            .Where(e => e.Entity.IsOn())
            .Subscribe(_ =>
            {
                // if (TooBright)
                // {
                //     remoteEntities.Bedroom4Remote.SendCommand("Light Power", "Bedroom 4 Fanco");
                // }

                fanEntities.Bedroom4Fan.TurnOn();

                fanEntities.Bedroom4Fan.SetPercentage(
                    (long)Math.Floor(fanEntities.Bedroom4Fan.Attributes?.PercentageStep ?? 16)
                );
            });

        climateEntities.Daikinap97235.StateChanges()
            .Where(s => s.Old.IsOff())
            .Subscribe(_ =>
            {
                if (inputBooleanEntities.Bedroom4Presence.IsOff()) return;

                fanEntities.Bedroom4Fan.SetPercentage(
                    (long)Math.Floor(fanEntities.Bedroom4Fan.Attributes?.PercentageStep ?? 16) * 5
                );

                netDaemonScheduler.RunIn(TimeSpan.FromMinutes(5),
                    () =>
                    {
                        fanEntities.Bedroom4Fan.SetPercentage(
                            (long)Math.Floor(fanEntities.Bedroom4Fan.Attributes?.PercentageStep ?? 16)
                        );
                    });
            });

        climateEntities.Daikinap97235.StateChanges()
            .Where(s => s.New.IsOff())
            .Subscribe(_ =>
            {
                if (inputBooleanEntities.Bedroom4Presence.IsOff()) return;

                netDaemonScheduler.RunIn(TimeSpan.FromHours(2),
                    () =>
                    {
                        fanEntities.Bedroom4Fan.SetPercentage(
                            (long)Math.Floor(fanEntities.Bedroom4Fan.Attributes?.PercentageStep ?? 16) * 2
                        );
                    });
            });

        inputBooleanEntities.Bedroom4Presence.StateChanges()
            .WhenStateIsFor(e => e.IsOff(), TimeSpan.FromSeconds(1), scheduler)
            .Subscribe(_ =>
            {
                // remoteEntities.Bedroom4Remote.SendCommand("Light Power", "Bedroom 4 Fanco");
                // remoteEntities.Bedroom4Remote.SendCommand("Power", "Bedroom 4 Fanco");

                fanEntities.Bedroom4Fan.TurnOff();
            });
    }
}