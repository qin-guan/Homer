using Homer.NetDaemon.Apps.Core;
using Homer.NetDaemon.Entities;
using NetDaemon.AppModel;

namespace Homer.NetDaemon.Apps.Bedroom4;

[Focus]
[NetDaemonApp]
public class Bedroom4Lights : Occupancy
{
    public Bedroom4Lights(
        InputDatetimeEntities inputDatetimeEntities,
        InputBooleanEntities inputBooleanEntities,
        BinarySensorEntities contactSensors,
        BinarySensorEntities motionSensors
    ) : base(
        inputDatetimeEntities.Bedroom4LastPresence,
        inputBooleanEntities.Bedroom4Presence,
        [contactSensors.Bedroom4DoorContact],
        [motionSensors.ScreekHumanSensor2a06ead0Zone1Presence]
    )
    {
    }
}