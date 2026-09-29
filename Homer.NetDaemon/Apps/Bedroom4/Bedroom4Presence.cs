using Homer.NetDaemon.Apps.Core;
using Homer.NetDaemon.Entities;
using NetDaemon.AppModel;

namespace Homer.NetDaemon.Apps.Bedroom4;

[NetDaemonApp]
public class Bedroom4Presence : Occupancy
{
    public Bedroom4Presence(
        InputDatetimeEntities inputDatetimeEntities,
        InputBooleanEntities inputBooleanEntities,
        BinarySensorEntities binarySensorEntities
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
    }
}
