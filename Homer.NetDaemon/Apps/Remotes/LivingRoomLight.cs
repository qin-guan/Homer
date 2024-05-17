using Homer.NetDaemon.Entities;
using Homer.ServiceDefaults.Metrics;
using NetDaemon.AppModel;
using NetDaemon.HassModel;

namespace Homer.NetDaemon.Apps.Remotes;

[NetDaemonApp]
public class LivingRoomLight
{
    public LivingRoomLight(
        IrRemoteLock irRemoteLock,
        InputBooleanEntities inputBooleanEntities,
        RemoteEntities remoteEntities
    )
    {
        var eventsProcessedMeter =
            EntityMetrics.MeterInstance.CreateCounter<int>("living_room_light_remote.events_processed");

        inputBooleanEntities.LivingRoomFanLights.StateChanges()
            .SubscribeAsync(async _ =>
            {
                eventsProcessedMeter.Add(1);
                await irRemoteLock.SemaphoreSlim.WaitAsync();
                await Task.Delay(1000);
                remoteEntities.LivingRoomRemote.SendCommand("Light Power", "Living Room KDK");
                await Task.Delay(1000);
                irRemoteLock.SemaphoreSlim.Release();
            });
    }
}