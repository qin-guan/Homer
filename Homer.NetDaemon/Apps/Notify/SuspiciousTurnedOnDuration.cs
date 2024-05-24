using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Homer.NetDaemon.Entities;
using Homer.ServiceDefaults.Metrics;
using NetDaemon.AppModel;
using NetDaemon.Client.HomeAssistant.Model;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace Homer.NetDaemon.Apps.Notify;

[NetDaemonApp]
public class SuspiciousTurnedOnDuration
{
    public SuspiciousTurnedOnDuration(
        IScheduler scheduler,
        ClimateEntities climateEntities,
        BinarySensorEntities binarySensorEntities,
        SwitchEntities switchEntities,
        InputBooleanEntities inputBooleanEntities,
        NotifyServices notifyServices
    )
    {
        var susCountMeter =
            EntityMetrics.MeterInstance.CreateCounter<int>("homer.netdaemon.suspicious_duration");

        foreach (var sensor in binarySensorEntities.EnumerateAll())
        {
            sensor.StateChanges()
                .WhenStateIsFor(e => e.IsOn(), TimeSpan.FromMinutes(45), scheduler)
                .Subscribe(e =>
                {
                    susCountMeter.Add(1, new[]
                    {
                        new KeyValuePair<string, object?>("friendly_name", e.Entity.Attributes?.FriendlyName)
                    });

                    notifyServices.MobileAppQinsIphone(
                        $"{e.Entity.Attributes?.FriendlyName} has been on for 45 minutes!"
                    );
                });
        }

        foreach (var entity in inputBooleanEntities.EnumerateAll())
        {
            entity.StateChanges()
                .WhenStateIsFor(e => e.IsOn(), TimeSpan.FromMinutes(45), scheduler)
                .Subscribe(e =>
                {
                    susCountMeter.Add(1, new[]
                    {
                        new KeyValuePair<string, object?>("friendly_name", e.Entity.Attributes?.FriendlyName)
                    });

                    notifyServices.MobileAppQinsIphone(
                        $"{e.Entity.Attributes?.FriendlyName} has been on for 45 minutes!"
                    );
                });
        }

        foreach (var entity in switchEntities.EnumerateAll())
        {
            entity.StateChanges()
                .WhenStateIsFor(e => e.IsOn(), TimeSpan.FromMinutes(45), scheduler)
                .Subscribe(e =>
                {
                    susCountMeter.Add(1, new[]
                    {
                        new KeyValuePair<string, object?>("friendly_name", e.Entity.Attributes?.FriendlyName)
                    });

                    notifyServices.MobileAppQinsIphone(
                        $"{e.Entity.Attributes?.FriendlyName} has been on for 45 minutes!"
                    );
                });
        }
    }
}