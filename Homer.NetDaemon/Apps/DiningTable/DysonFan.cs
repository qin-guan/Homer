using System.Reactive.Concurrency;
using Homer.NetDaemon.Apps.Remotes;
using Homer.NetDaemon.Entities;
using Homer.ServiceDefaults.Metrics;
using NetDaemon.AppModel;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace Homer.NetDaemon.Apps.DiningTable;

[NetDaemonApp]
public class DysonFan : IAsyncInitializable
{
    private readonly BinarySensorEntity _presence;
    private readonly SwitchEntity _switch;

    public DysonFan(
        IScheduler scheduler,
        IrRemoteLock irRemoteLock,
        SwitchEntities switchEntities,
        BinarySensorEntities binarySensorEntities,
        RemoteEntities remoteEntities
    )
    {
        _presence = binarySensorEntities.PresenceSensorFp2B4c4PresenceSensor5;
        _switch = switchEntities.LivingRoomIkeaPlug;

        var eventsProcessedMeter =
            EntityMetrics.MeterInstance.CreateCounter<int>("homer.netdaemon.dyson_fan.events_processed");

        _presence.StateChanges()
            .WhenStateIsFor(e =>
            {
                eventsProcessedMeter.Add(1);
                return e.IsOn();
            }, TimeSpan.FromSeconds(45), scheduler)
            .SubscribeAsync(async e =>
            {
                _switch.TurnOn();
                await Task.Delay(500);
                await irRemoteLock.LivingRoom.WaitAsync();
                remoteEntities.LivingRoomRemote.SendCommand("Power", "Dyson");
                await Task.Delay(500);
                irRemoteLock.LivingRoom.Release();
            });

        _presence.StateChanges()
            .WhenStateIsFor(e =>
            {
                eventsProcessedMeter.Add(1);
                return e.IsOff();
            }, TimeSpan.FromSeconds(30), scheduler)
            .Subscribe(e => { if (_presence.IsOff()) _switch.TurnOff(); });
    }

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (_presence.IsOff())
        {
            _switch.TurnOff();
        }

        return Task.CompletedTask;
    }
}