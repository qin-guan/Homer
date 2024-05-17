using Homer.NetDaemon.Entities;
using Homer.ServiceDefaults.Metrics;
using NetDaemon.AppModel;
using NetDaemon.HassModel;

namespace Homer.NetDaemon.Apps.Remotes;

[NetDaemonApp]
public class LivingRoomFan
{
    public LivingRoomFan(
        IrRemoteLock irRemoteLock,
        InputBooleanEntities inputBooleanEntities,
        RemoteEntities remoteEntities
    )
    {
        var eventsProcessedMeter = EntityMetrics.MeterInstance.CreateCounter<int>("living_room_fan_remote.events_processed");
        
        inputBooleanEntities.LivingRoomFan.StateChanges()
            .SubscribeAsync(async _ =>
            {
                eventsProcessedMeter.Add(1);
                await irRemoteLock.SemaphoreSlim.WaitAsync();
                await Task.Delay(1000);
                remoteEntities.LivingRoomRemote.SendCommand("Power", "Living Room KDK");
                await Task.Delay(1000);
                irRemoteLock.SemaphoreSlim.Release();
            });
    }
}