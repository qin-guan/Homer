using Homer.NetDaemon.Apps.Kdk;
using Homer.NetDaemon.Entities;
using Homer.ServiceDefaults.Metrics;
using NetDaemon.AppModel;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace Homer.NetDaemon.Apps.Remotes;

[NetDaemonApp]
public class LivingRoomLight
{
    public LivingRoomLight(
        InputBooleanEntities inputBooleanEntities,
        RemoteEntities remoteEntities,
        IKdkApi kdkApi
    )
    {
        var eventsProcessedMeter =
            EntityMetrics.MeterInstance.CreateCounter<int>("homer.netdaemon.living_room_light_remote.events_processed");

        inputBooleanEntities.LivingRoomFanLights.StateChanges()
            .SubscribeAsync(async e =>
            {
                eventsProcessedMeter.Add(1);
                await kdkApi.SetDeviceControlsAsync(
                    new KdkApiPostDeviceControlsRequest(
                        KdkAppliances.LivingRoomFan,
                        e.New.IsOff() ? KdkPackets.TurnLightOff : KdkPackets.TurnLightOn
                    )
                );
            });
    }
}