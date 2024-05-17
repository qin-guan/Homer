using System.Reactive.Concurrency;
using Homer.NetDaemon.Entities;
using Homer.ServiceDefaults.Metrics;
using NetDaemon.AppModel;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace Homer.NetDaemon.Apps.DiningTable;

[NetDaemonApp]
public class DiningTableLight
{
    public DiningTableLight(IScheduler scheduler, BinarySensorEntities binarySensorEntities, SwitchEntities switchEntities)
    {
        var eventsProcessedMeter =
            EntityMetrics.MeterInstance.CreateCounter<int>("dining_table_light.events_processed");
        
        binarySensorEntities.PresenceSensorFp2B4c4PresenceSensor5.StateChanges()
            .WhenStateIsFor(e =>
            {
                eventsProcessedMeter.Add(1);
                return e.IsOff();
            }, TimeSpan.FromMinutes(1), scheduler)
            .Subscribe(e =>
            {
                switchEntities.DiningTableLights.TurnOff();
            });
    }
}