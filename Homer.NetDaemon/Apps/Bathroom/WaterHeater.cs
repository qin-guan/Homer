using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Homer.NetDaemon.Apps.Daikin;
using Homer.NetDaemon.Entities;
using NetDaemon.AppModel;
using NetDaemon.Extensions.Scheduler;
using NetDaemon.HassModel;

namespace Homer.NetDaemon.Apps.Bathroom;

// [Focus]
[NetDaemonApp]
public class WaterHeater
{
    public WaterHeater(IDaikinApi daikinApi, ClimateEntities climateEntities, IScheduler scheduler)
    {
        climateEntities.WaterHeater.StateAllChanges()
            .Where(e => e.New?.State == "heat")
            .SubscribeAsync(async _ =>
            {
                await daikinApi.UpdateDeviceStatusAsync(948994, new DaikinApiPostDeviceStatus(
                    new DaikinApiPostDeviceStatus.InnerParameters(
                        HeaterStatus: "ON"
                    )
                ));
            });

        climateEntities.WaterHeater.StateAllChanges()
            .Where(e => e.New?.State == "off")
            .SubscribeAsync(async _ =>
            {
                await daikinApi.UpdateDeviceStatusAsync(948994, new DaikinApiPostDeviceStatus(
                    new DaikinApiPostDeviceStatus.InnerParameters(
                        HeaterStatus: "OFF"
                    )
                ));
            });

        climateEntities.WaterHeater.StateAllChanges()
            .Where(e => e.New?.Attributes?.Temperature != e.Old?.Attributes?.Temperature)
            .SubscribeAsync(async e =>
            {
                await daikinApi.UpdateDeviceStatusAsync(948994, new DaikinApiPostDeviceStatus(
                    new DaikinApiPostDeviceStatus.InnerParameters(
                        (int?)e.New?.Attributes?.Temperature ?? throw new ArgumentNullException()
                    )
                ));
            });

        scheduler.ScheduleCron("0 23 * * *", () =>
        {
            climateEntities.WaterHeater.SetHvacMode("off");
        });
        
        scheduler.ScheduleCron("0 18 * * *", () =>
        {
            climateEntities.WaterHeater.SetHvacMode("heat");
        });
    }
}