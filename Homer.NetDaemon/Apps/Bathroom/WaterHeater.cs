using System.Reactive.Linq;
using Homer.NetDaemon.Apps.Daikin;
using Homer.NetDaemon.Entities;
using NetDaemon.AppModel;
using NetDaemon.HassModel;

namespace Homer.NetDaemon.Apps.Bathroom;

[Focus]
[NetDaemonApp]
public class WaterHeater
{
    public WaterHeater(IDaikinApi daikinApi, ClimateEntities climateEntities)
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
    }
}