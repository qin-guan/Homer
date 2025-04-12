using Homer.NetDaemon.Apps.Kdk;
using Homer.NetDaemon.Entities;
using Homer.ServiceDefaults.Metrics;
using NetDaemon.AppModel;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace Homer.NetDaemon.Apps.Remotes;

// [Focus]
[NetDaemonApp]
public class LivingRoomFan
{
    public LivingRoomFan(
        InputBooleanEntities inputBooleanEntities,
        RemoteEntities remoteEntities,
        IKdkApi kdkApi
    )
    {
        var eventsProcessedMeter =
            EntityMetrics.MeterInstance.CreateCounter<int>("homer.netdaemon.living_room_fan_remote.events_processed");

        inputBooleanEntities.LivingRoomFan.StateChanges()
            .SubscribeAsync(async e =>
            {
                eventsProcessedMeter.Add(1);
                await kdkApi.SetDeviceControlsAsync(
                    new KdkApiPostDeviceControlsRequest(
                        KdkAppliances.LivingRoomFan,
                        inputBooleanEntities.LivingRoomFanLights.State switch
                        {
                            "on" => e.New.IsOff() ? KdkPackets.TurnOffWhileLightOn : KdkPackets.TurnOnWhileLightOn,
                            "off" => e.New.IsOff() ? KdkPackets.TurnOffWhileLightOff : KdkPackets.TurnOnWhileLightOff,
                            _ => throw new ArgumentOutOfRangeException(
                                $"Invalid Living Room Fan Lights State {inputBooleanEntities.LivingRoomFanLights.State}"
                            )
                        }
                    )
                );
            });
    }
}