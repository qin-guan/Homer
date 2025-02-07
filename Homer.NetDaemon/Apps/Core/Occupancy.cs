using System.Reactive.Linq;
using Homer.NetDaemon.Entities;
using NetDaemon.HassModel.Entities;

namespace Homer.NetDaemon.Apps.Core;

public abstract class Occupancy
{
    private readonly InputDatetimeEntity _lastPresence;
    private readonly InputBooleanEntity _presence;
    private readonly List<BinarySensorEntity> _contactSensors;
    private readonly List<BinarySensorEntity> _motionSensors;
    private readonly List<BinarySensorEntity> _triggerSensors;
    private readonly TimeSpan _delay = TimeSpan.FromSeconds(12);

    private bool DoorClosed => _contactSensors.All(e => e.IsOff());

    private DateTime LastPresenceDateTime
    {
        get
        {
            if (_lastPresence.Attributes?.Year is null) return default;
            if (_lastPresence.Attributes.Month is null) return default;
            if (_lastPresence.Attributes.Day is null) return default;
            if (_lastPresence.Attributes.Hour is null) return default;
            if (_lastPresence.Attributes.Minute is null) return default;
            if (_lastPresence.Attributes.Second is null) return default;

            return new DateTime((int)_lastPresence.Attributes.Year.Value,
                (int)_lastPresence.Attributes.Month.Value, (int)_lastPresence.Attributes.Day.Value,
                (int)_lastPresence.Attributes.Hour.Value, (int)_lastPresence.Attributes.Minute.Value,
                (int)_lastPresence.Attributes.Second.Value);
        }
    }
    
    public Occupancy(
        InputDatetimeEntity lastPresence,
        InputBooleanEntity presence,
        List<BinarySensorEntity> contactSensors,
        List<BinarySensorEntity> motionAndTriggerSensors,
        TimeSpan delay
    ) : this(lastPresence, presence, contactSensors, motionAndTriggerSensors, motionAndTriggerSensors)
    {
        _delay = delay;
    }

    public Occupancy(
        InputDatetimeEntity lastPresence,
        InputBooleanEntity presence,
        List<BinarySensorEntity> contactSensors,
        List<BinarySensorEntity> triggerSensors,
        List<BinarySensorEntity> motionSensors,
        TimeSpan delay
    ) : this(lastPresence, presence, contactSensors, motionSensors, triggerSensors)
    {
        _delay = delay;
    }

    public Occupancy(
        InputDatetimeEntity lastPresence,
        InputBooleanEntity presence,
        List<BinarySensorEntity> contactSensors,
        List<BinarySensorEntity> triggerSensors,
        List<BinarySensorEntity> motionSensors
    )
    {
        _lastPresence = lastPresence;
        _presence = presence;
        _contactSensors = contactSensors;
        _triggerSensors = triggerSensors;
        _motionSensors = motionSensors;

        var contactSensorObservables = _contactSensors.Select(e => e.StateChanges()).Merge().DistinctUntilChanged();
        var triggerSensorObservables = _triggerSensors.Select(e => e.StateChanges()).Merge().DistinctUntilChanged();
        var motionSensorObservables = _motionSensors.Select(e => e.StateChanges()).Merge();

        contactSensorObservables
            .Where(e => e.New.IsOff())
            .Subscribe(e => { Logic(OccupancyInvocationSource.DoorOpened); });

        triggerSensorObservables
            .Where(e => e.New.IsOn())
            .Subscribe(_ =>
            {
                if (_presence.IsOff())
                {
                    _presence.TurnOn();
                }

                _lastPresence.SetDatetime(null, null, null, DateTimeOffset.Now.ToUnixTimeSeconds());
            });

        motionSensorObservables
            .Where(e => e.New.IsOn() || e.New?.State == "Home")
            .Subscribe(_ =>
            {
                _lastPresence.SetDatetime(null, null, null, DateTimeOffset.Now.ToUnixTimeSeconds());
            });

        motionSensorObservables
            .Where(e => e.New.IsOff())
            .Subscribe(_ => { Logic(OccupancyInvocationSource.MotionCleared); });
    }

    private void Logic(OccupancyInvocationSource invocationSource)
    {
        var motionLastChanged = _motionSensors.Max(e => e.EntityState?.LastChanged) ?? default;
        var contactSensorsLastChanged = _contactSensors.Max(e => e.EntityState?.LastChanged) ?? default;

        if (_presence.IsOn() &&
            (
                (invocationSource == OccupancyInvocationSource.MotionCleared && (
                        !DoorClosed || (DoorClosed &&
                                        LastPresenceDateTime < contactSensorsLastChanged &&
                                        motionLastChanged.Subtract(_delay) <=
                                        contactSensorsLastChanged
                        ))
                ) ||
                (invocationSource == OccupancyInvocationSource.DoorOpened && _presence.IsOff())
            ))
        {
            _presence.TurnOff();
        }
    }
}