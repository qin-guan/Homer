using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Homer.NetDaemon.Apps.Core;
using Homer.NetDaemon.Entities;
using NetDaemon.AppModel;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace Homer.NetDaemon.Apps.Bathroom;

[NetDaemonApp]
public class BathroomPresence : Occupancy
{
    private readonly List<NumericSensorEntity> _lightSensors;
    private readonly SwitchEntities _switchEntities;
    private bool TooBright => _lightSensors.Min(e => e.State) > 25;

    private List<SwitchEntity> Switches
    {
        get
        {
            return TimeOnly.FromDateTime(DateTime.Now) switch
            {
                var value when value >= new TimeOnly(0, 0) && value < new TimeOnly(6, 0) =>
                [
                    _switchEntities.BathroomLightsRight
                ],

                var value when value >= new TimeOnly(6, 0) && value < new TimeOnly(18, 0) =>
                [
                    _switchEntities.BathroomLightsCenter
                ],

                var value when value >= new TimeOnly(18, 0) && value < new TimeOnly(23, 59) =>
                [
                    _switchEntities.BathroomLightsCenter, _switchEntities.BathroomLightsLeft
                ],

                _ => [_switchEntities.BathroomLightsRight]
            };
        }
    }

    public BathroomPresence(
            ILogger<BathroomPresence> logger,
        IScheduler scheduler,
        SensorEntities sensorEntities,
        InputDatetimeEntities inputDatetimeEntities,
        InputBooleanEntities inputBooleanEntities,
        BinarySensorEntities contactSensors,
        BinarySensorEntities motionSensors,
        SwitchEntities switchEntities
    ) : base(
        inputDatetimeEntities.BathroomLastPresence,
        inputBooleanEntities.BathroomPresence,
        [contactSensors.BathroomDoorContact],
        [motionSensors.BathroomTuyaPresencePresence, motionSensors.BathroomSinkMotionOccupancy],
        TimeSpan.FromSeconds(30)
    )
    {
        _switchEntities = switchEntities;

        _lightSensors =
        [
            sensorEntities.BathroomSinkMotionIlluminance,
            sensorEntities.BathroomDoorMotionIlluminance
        ];

        inputBooleanEntities.BathroomPresence.StateChanges()
            .Where(e => e.Entity.IsOn())
            .Subscribe(_ =>
            {
                if (TooBright)
                {
                    logger.LogInformation("Too bright, therefore skipping turning on lights");
                    return;
                }

                foreach (var light in Switches)
                {
                    logger.LogInformation("Turning on light {EntityId}", light.EntityId);
                    light.TurnOn();
                }
            });

        inputBooleanEntities.BathroomPresence.StateChanges()
            .WhenStateIsFor(e => e.IsOff(), TimeSpan.FromMinutes(1.5), scheduler)
            .Subscribe(_ =>
            {
                logger.LogInformation("Turning off lights");
                switchEntities.BathroomLightsCenter.TurnOff();
                switchEntities.BathroomLightsLeft.TurnOff();
                switchEntities.BathroomLightsRight.TurnOff();
            });
    }
}
