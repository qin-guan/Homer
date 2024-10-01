using Homer.NetDaemon.Entities;
using Homer.ServiceDefaults.Metrics;
using NetDaemon.AppModel;
using NetDaemon.HassModel;

namespace Homer.NetDaemon.Apps.Remotes;

[NetDaemonApp]
public class LivingRoomLight
{
    public LivingRoomLight(
        IrRemoteChannel irRemoteChannel,
        InputBooleanEntities inputBooleanEntities,
        RemoteEntities remoteEntities
    )
    {
        var eventsProcessedMeter =
            EntityMetrics.MeterInstance.CreateCounter<int>("homer.netdaemon.living_room_light_remote.events_processed");

        inputBooleanEntities.LivingRoomFanLights.StateChanges()
            .SubscribeAsync(async _ =>
            {
                eventsProcessedMeter.Add(1);
                await irRemoteChannel.LivingRoomChannel.Writer.WriteAsync(LivingRoomRemoteCommand.Light);
            });
    }
}