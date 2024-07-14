using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Homer.NetDaemon.Apps.Core;
using Homer.NetDaemon.Entities;
using NetDaemon.AppModel;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace Homer.NetDaemon.Apps.MasterBathroom;

[NetDaemonApp]
public class MasterBathroomPresence : Occupancy
{
    private readonly List<NumericSensorEntity> _lightSensors;
    private readonly SwitchEntities _switchEntities;
    private bool TooBright => _lightSensors.Min(e => e.State) > 100;

    private List<SwitchEntity> Switches
    {
        get
        {
            return TimeOnly.FromDateTime(DateTime.Now) switch
            {
                var value when value >= new TimeOnly(0, 0) && value < new TimeOnly(6, 0) =>
                [
                    _switchEntities.MasterBathroomLightsCenter
                ],

                var value when value >= new TimeOnly(6, 0) && value < new TimeOnly(18, 0) =>
                [
                    _switchEntities.MasterBathroomLightsLeft
                ],

                var value when value >= new TimeOnly(18, 0) && value < new TimeOnly(23, 59) =>
                [
                    _switchEntities.MasterBathroomLightsLeft, _switchEntities.MasterBathroomLightsRight
                ],

                _ => [_switchEntities.MasterBathroomLightsLeft]
            };
        }
    }

    public MasterBathroomPresence(
        IScheduler scheduler,
        SensorEntities sensorEntities,
        InputDatetimeEntities inputDatetimeEntities,
        InputBooleanEntities inputBooleanEntities,
        BinarySensorEntities contactSensors,
        BinarySensorEntities motionSensors,
        SwitchEntities switchEntities
    ) : base(
        inputDatetimeEntities.MasterBathroomLastPresence,
        inputBooleanEntities.MasterBathroomPresence,
        [contactSensors.MasterBathroomDoorContact],
        [motionSensors.MasterBathroomSinkMotionOccupancy, motionSensors.MasterBathroomToiletMotionOccupancy],
        TimeSpan.FromSeconds(12)
    )
    {
        _switchEntities = switchEntities;

        _lightSensors =
        [
            sensorEntities.MasterBathroomSinkMotionIlluminanceLux,
            sensorEntities.MasterBathroomToiletMotionIlluminanceLux
        ];

        inputBooleanEntities.MasterBathroomPresence.StateChanges()
            .Where(e => e.Entity.IsOn())
            .Subscribe(_ =>
            {
                if (TooBright) return;

                foreach (var light in Switches)
                {
                    light.TurnOn();
                }
            });

        inputBooleanEntities.MasterBathroomPresence.StateChanges()
            .WhenStateIsFor(e => e.IsOff(), TimeSpan.FromMinutes(1.5), scheduler)
            .Subscribe(_ =>
            {
                switchEntities.MasterBathroomLightsLeft.TurnOff();
                switchEntities.MasterBathroomLightsCenter.TurnOff();
                switchEntities.MasterBathroomLightsRight.TurnOff();
            });
    }
}