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
    public bool TooBright => _sensorEntities.ScreekHumanSensor2a06ead0Illuminance.State > 0;
    public bool IsMidnight => TimeOnly.FromDateTime(DateTime.Now).IsBetween(new TimeOnly(2, 0), new TimeOnly(6, 0));

    public Bedroom4Lights(
        ILogger<Bedroom4Lights> logger,
        IScheduler scheduler,
        INetDaemonScheduler netDaemonScheduler,
        InputDatetimeEntities inputDatetimeEntities,
        InputBooleanEntities inputBooleanEntities,
        InputNumberEntities inputNumberEntities,
        BinarySensorEntities binarySensorEntities,
        SensorEntities sensorEntities,
        SwitchEntities switchEntities,
        RemoteEntities remoteEntities,
        ClimateEntities climateEntities,
        FanEntities fanEntities,
        EventEntities eventEntities
    ) : base(
        inputDatetimeEntities.Bedroom4LastPresence,
        inputBooleanEntities.Bedroom4Presence,
        [binarySensorEntities.Bedroom4DoorContact],
        [
            binarySensorEntities.ScreekHumanSensor2a06ead0Zone1Presence,
        ],
        [
            binarySensorEntities.ScreekHumanSensor2a06ead0Zone1Presence,
        ],
        TimeSpan.FromSeconds(2)
    )
    {
        _sensorEntities = sensorEntities;

        eventEntities.Bedroom4LightsAction.StateChanges()
            .SubscribeAsync(async e =>
            {
                logger.LogInformation("Bedroom 4 light action");

                if (switchEntities.Bedroom4Lights.IsOff())
                {
                    logger.LogInformation("Bedroom 4 light action turning on presence");
                    inputBooleanEntities.Bedroom4Presence.TurnOn();
                }
                else
                {
                    logger.LogInformation("Bedroom 4 light action turning off presence");
                    inputBooleanEntities.Bedroom4Light.TurnOff();
                    inputBooleanEntities.Bedroom4Fan.TurnOff();

                    await Task.Delay(3000);

                    switchEntities.Bedroom4Lights.TurnOff();
                }
            });

        inputBooleanEntities.Bedroom4Presence.StateChanges().DistinctUntilChanged()
            .SubscribeAsync(async _ =>
            {
                switchEntities.Bedroom4Lights.TurnOn();

                await Task.Delay(1000);

                if (!TooBright && !IsMidnight)
                {
                    inputBooleanEntities.Bedroom4Light.TurnOn();
                }

                inputBooleanEntities.Bedroom4Fan.TurnOn();
            });

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

        inputBooleanEntities.Bedroom4Presence.StateChanges()
            .WhenStateIsFor(e => e.IsOff(), TimeSpan.FromMinutes(3), scheduler)
            .SubscribeAsync(async _ =>
            {
                inputBooleanEntities.Bedroom4Light.TurnOff();
                inputBooleanEntities.Bedroom4Fan.TurnOff();

                await Task.Delay(3000);

                switchEntities.Bedroom4Lights.TurnOff();
            });
    }
}