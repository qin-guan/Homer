// using System.Reactive.Linq;
// using Homer.NetDaemon.Entities;
// using Homer.NetDaemon.Presence;
// using NetDaemon.AppModel;
// using NetDaemon.HassModel.Entities;
//
// namespace Homer.NetDaemon.Apps.Bathroom;
//
// [NetDaemonApp]
// public class BathroomPresence
// {
//     private WaspState WaspState = WaspState.NoWasp;
//     private bool Occupied = false;
//     private DateTime LastMotion;
//
//     public BathroomPresence(BinarySensorEntities binarySensorEntities, SwitchEntities switchEntities)
//     {
//         var motionSensors = new List<BinarySensorEntity>
//         {
//             binarySensorEntities.BathroomSinkMotionOccupancy,
//             binarySensorEntities.BathroomDoorMotionOccupancy
//         };
//
//         var contactSensors = new List<BinarySensorEntity>
//         {
//             binarySensorEntities.BathroomContactSensorContact
//         };
//
//         // Door opened
//         contactSensors.Select(e => e.StateChanges()).Merge()
//             .Where(e => e.New.IsOff())
//             .Subscribe(e => { });
//
//         // Motion detected
//         motionSensors.Select(e => e.StateChanges()).Merge()
//             .Where(e => e.New.IsOn())
//             .Subscribe(e =>
//             {
//                 LastMotion = DateTime.Now;
//
//                 if (Occupied) return;
//
//                 Occupied = true;
//                 switchEntities.BathroomLightsCenter.TurnOn();
//             });
//
//         // Motion cleared
//         motionSensors.Select(e => e.StateChanges()).Merge()
//             .Where(e => e.New.IsOff())
//             .Subscribe(e =>
//             {
//                 if (WaspState == WaspState.Wasp)
//                 {
//                     WaspState = WaspState.WaspInBox;
//                 }
//             });
//
//         void Trigger(bool isFromClear)
//         {
//             if (Occupied && (((isFromClear) && ((contactSensors.Any(e => e.IsOn())) ||
//                                                 (contactSensors.All(e => e.IsOff()) &&
//                                                  LastMotion < contactSensors.Max(e => e.EntityState?.LastChanged) &&
//                                                  (motionSensors.Max(e => e.EntityState.LastChanged).Value
//                                                      .Subtract(TimeSpan.FromSeconds(10)))))) || ()))
//         }
//     }
// }