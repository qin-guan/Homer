using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Homer.NetDaemon.Entities;
using NetDaemon.AppModel;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace Homer.NetDaemon.Apps.Bedroom4;

/// <summary>Turns the Bedroom 4 ceiling fan and its light on with presence and off once the room is empty.</summary>
[NetDaemonApp]
public class Bedroom4Lights
{
    private readonly SensorEntities _sensorEntities;

    public bool TooBright => _sensorEntities.ScreekHumanSensor2a06ead0Illuminance.State > 0;

    public bool IsMidnight => TimeOnly.FromDateTime(DateTime.Now).IsBetween(new TimeOnly(2, 0), new TimeOnly(6, 0));

    public Bedroom4Lights(
        IScheduler scheduler,
        InputBooleanEntities inputBooleanEntities,
        SensorEntities sensorEntities,
        SwitchEntities switchEntities
    )
    {
        _sensorEntities = sensorEntities;

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
