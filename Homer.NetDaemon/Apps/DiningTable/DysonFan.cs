using System.Reactive.Concurrency;
using System.Reactive.Linq;
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
    private readonly List<BinarySensorEntity> _presence;
    private readonly SwitchEntity _switch;

    public DysonFan(
        IScheduler scheduler,
        SwitchEntities switchEntities,
        BinarySensorEntities binarySensorEntities,
        RemoteEntities remoteEntities
    )
    {
        _presence =
        [
            binarySensorEntities.PresenceSensorFp2B4c4PresenceSensor5,
            binarySensorEntities.PresenceSensorFp2B4c4PresenceSensor4
        ];
        _switch = switchEntities.LivingRoomIkeaPlug;


        _presence.Select(e => e.StateChanges()).Merge().DistinctUntilChanged()
            .WhenStateIsFor(e =>
            {
                EntityMetrics.AutomationEvent("dyson_fan");
                return e.IsOn();
            }, TimeSpan.FromSeconds(45), scheduler)
            .SubscribeAsync(async e =>
            {
                _switch.TurnOn();
                await Channels.Channels.LivingRoomChannel.Writer.WriteAsync(LivingRoomRemoteCommand.Dyson);
            });

        _presence.StateChanges()
            .WhenStateIsFor(e =>
            {
                EntityMetrics.AutomationEvent("dyson_fan");
                return e.IsOff();
            }, TimeSpan.FromMinutes(8), scheduler)
            .SubscribeAsync(async e =>
            {
                if (_presence.All(e => e.IsOff()))
                {
                    _switch.TurnOff();
                    await Task.Delay(10000);
                    _switch.TurnOn();
                }
            });
    }

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (_presence.All(e => e.IsOff()))
        {
            _switch.TurnOff();
        }

        return Task.CompletedTask;
    }
}
