using System.Reactive.Linq;
using Homer.NetDaemon.Entities;
using NetDaemon.AppModel;
using NetDaemon.HassModel.Entities;

namespace Homer.NetDaemon.Apps.LivingRoom;

[NetDaemonApp]
public class LivingRoomLights
{
    public LivingRoomLights(ILogger<LivingRoomLights> logger, BinarySensorEntities binarySensorEntities, RemoteEntities remoteEntities)
    {
        List<BinarySensorEntity> fanSensors =
        [
            binarySensorEntities.PresenceSensorFp2B4c4PresenceSensor2,
            binarySensorEntities.PresenceSensorFp2B4c4PresenceSensor3,
            binarySensorEntities.PresenceSensorFp2B4c4PresenceSensor4,
            binarySensorEntities.PresenceSensorFp2B4c4PresenceSensor5,
            binarySensorEntities.PresenceSensorFp2B4c4PresenceSensor6,
            binarySensorEntities.PresenceSensorFp2B4c4PresenceSensor8,
        ];

        foreach (var sensor in fanSensors)
        {
            sensor.StateChanges()
                .Where(s => s.New.IsOn())
                .Subscribe(_ =>
                {
                    remoteEntities.LivingRoomRemote.SendCommand("Light Power", "Living Room KDK");
                });
        }
    }
}