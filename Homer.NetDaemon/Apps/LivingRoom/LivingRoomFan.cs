using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Homer.NetDaemon.Entities;
using NetDaemon.AppModel;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace Homer.NetDaemon.Apps.LivingRoom;

[NetDaemonApp]
public class LivingRoomFan
{
    public LivingRoomFan(ILogger<LivingRoomFan> logger, IScheduler scheduler, BinarySensorEntities binarySensorEntities,
        RemoteEntities remoteEntities)
    {
        List<BinarySensorEntity> fanSensors =
        [
            binarySensorEntities.PresenceSensorFp2B4c4PresenceSensor2,
            binarySensorEntities.PresenceSensorFp2B4c4PresenceSensor3,
        ];

        var noPresence = fanSensors.All(f => f.IsOff());

        foreach (var sensor in fanSensors)
        {
            sensor.StateChanges()
                .WhenStateIsFor(s => s.IsOn(), TimeSpan.FromSeconds(10), scheduler)
                .Subscribe(_ => { remoteEntities.LivingRoomRemote.SendCommand("Power", "Living Room KDK"); });
        }
    }
}