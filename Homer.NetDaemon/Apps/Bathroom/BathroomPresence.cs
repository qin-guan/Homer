using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Homer.NetDaemon.Entities;
using Homer.NetDaemon.Presence;
using NetDaemon.AppModel;
using NetDaemon.Extensions.Scheduler;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace Homer.NetDaemon.Apps.Bathroom;

[Focus]
[NetDaemonApp]
public class BathroomPresence
{
    private string CurrentWaspState = Presence.WaspState.STATE_NO_WASP_IN_BOX;
    private readonly TimeSpan Delay = TimeSpan.FromSeconds(10);
    private readonly List<BinarySensorEntity> boxSensors;
    private readonly List<BinarySensorEntity> waspSensors;

    private bool Occupied = false;
    private DateTime LastMotion;

    public BathroomPresence(INetDaemonScheduler scheduler, BinarySensorEntities binarySensorEntities,
        SwitchEntities switchEntities)
    {
        waspSensors =
        [
            binarySensorEntities.BathroomSinkMotionOccupancy,
            binarySensorEntities.BathroomDoorMotionOccupancy
        ];

        boxSensors =
        [
            binarySensorEntities.BathroomContactSensorContact
        ];

        boxSensors.Select(e => e.StateChanges()).Merge().Subscribe(e =>
        {
            CurrentWaspState = WaspState.STATE_NO_WASP_IN_BOX;
            scheduler.RunIn(Delay, () => { WaspInABox(e.New.State, getWaspState()); });
        });

        waspSensors.Select(e => e.StateChanges())
            .Merge()
            .Subscribe(e => { WaspInABox(getBoxState(), e.New.State); });

        WaspInABox(getBoxState(), getWaspState());
    }

    private void WaspInABox(string boxState, string waspState)
    {
        if (waspState == WaspState.STATE_WASP)
        {
            CurrentWaspState = WaspState.STATE_WASP_IN_BOX;
            Console.WriteLine("{0}{1}{2}", boxState, waspState, CurrentWaspState);
        }
        else if (boxState == WaspState.STATE_BOX_OPEN)
        {
            CurrentWaspState = WaspState.STATE_NO_WASP_IN_BOX;
            Console.WriteLine("{0}{1}{2}", boxState, waspState, CurrentWaspState);
        }
    }

    private string getBoxState()
    {
        return boxSensors.Any(entity => entity.IsOff()) ? WaspState.STATE_BOX_OPEN : WaspState.STATE_BOX_CLOSED;
    }

    private string getWaspState()
    {
        return waspSensors.Any(entity => entity.IsOn()) ? WaspState.STATE_WASP : WaspState.STATE_NO_WASP;
    }
}