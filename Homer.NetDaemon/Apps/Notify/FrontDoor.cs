using System.Reactive.Concurrency;
using Homer.NetDaemon.Entities;
using NetDaemon.AppModel;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace Homer.NetDaemon.Apps.Notify;

[NetDaemonApp]
public class FrontDoor
{
    public FrontDoor(
        NotifyServices notifyServices,
        SensorEntities sensorEntities,
        BinarySensorEntities binarySensorEntities,
        SwitchEntities switchEntities,
        IScheduler scheduler
    )
    {
        binarySensorEntities.FrontDoorContact.StateAllChanges().Subscribe((e) =>
        {
            notifyServices.MobileAppQinsIphone(
                $"Front door was {(e.New.IsOn() ? "opened" : "closed")}"
            );
        });
    }
}
