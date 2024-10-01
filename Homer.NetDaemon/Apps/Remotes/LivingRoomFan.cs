using Homer.NetDaemon.Entities;
using Homer.ServiceDefaults.Metrics;
using NetDaemon.AppModel;
using NetDaemon.HassModel;

namespace Homer.NetDaemon.Apps.Remotes;

[NetDaemonApp]
public class LivingRoomFan
{
    public LivingRoomFan(
        IrRemoteChannel irRemoteChannel,
        InputBooleanEntities inputBooleanEntities,
        RemoteEntities remoteEntities)
    {
        var eventsProcessedMeter =
            EntityMetrics.MeterInstance.CreateCounter<int>("homer.netdaemon.living_room_fan_remote.events_processed");

        inputBooleanEntities.LivingRoomFan.StateChanges()
            .SubscribeAsync(async _ =>
            {
                eventsProcessedMeter.Add(1);
                await irRemoteChannel.LivingRoomChannel.Writer.WriteAsync(LivingRoomRemoteCommand.Fan);
            });
    }
}