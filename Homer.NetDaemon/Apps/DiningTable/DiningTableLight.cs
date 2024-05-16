using System.Reactive.Concurrency;
using Homer.NetDaemon.Entities;
using NetDaemon.AppModel;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace Homer.NetDaemon.Apps.DiningTable;

[NetDaemonApp]
public class DiningTableLight
{
    public DiningTableLight(IScheduler scheduler, BinarySensorEntities binarySensorEntities, SwitchEntities switchEntities)
    {
        binarySensorEntities.PresenceSensorFp2B4c4PresenceSensor5.StateChanges()
            .WhenStateIsFor(e => e.IsOff(), TimeSpan.FromMinutes(1), scheduler)
            .Subscribe(e =>
            {
                switchEntities.DiningTableLights.TurnOff();
            });
    }
}