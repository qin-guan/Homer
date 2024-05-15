// using System.Reactive.Linq;
// using Homer.NetDaemon.Entities;
// using NetDaemon.HassModel.Entities;
//
// namespace Homer.NetDaemon.Presence;
//
// public class Presence
// {
//     private readonly IList<BinarySensorEntity> _motionSensors;
//     private readonly IList<BinarySensorEntity> _doorSensors;
//     private readonly Action _onPresenceDetected;
//     private readonly Action _onPresenceCleared;
//
//     public WaspState WaspState = WaspState.NoWasp;
//
//     public bool MotionDetected => _motionSensors.Any(m => m.IsOn());
//     public bool DoorClosed => _doorSensors.Any(d => d.IsOn());
//
//     public Presence(
//         IList<BinarySensorEntity> motionSensors,
//         IList<BinarySensorEntity> doorSensors,
//         Action onPresenceDetected,
//         Action onPresenceCleared
//     )
//     {
//         _motionSensors = motionSensors.StateChanges();
//         _doorSensors = doorSensors;
//         _onPresenceDetected = onPresenceDetected;
//         _onPresenceCleared = onPresenceCleared;
//
//         motionSensors
//             .Select(e => e.StateChanges())
//             .Merge()
//             .Subscribe(e =>
//             {
//                 if (DoorClosed)
//                 {
//                     if (MotionDetected)
//                     {
//                         _onPresenceDetected();
//                         WaspState = WaspState.WaspInBox;
//                     }
//                     else
//                     {
//                         _onPresenceCleared();
//                         WaspState = WaspState.NoWaspInBox;
//                     }
//                 }
//                 else
//                 {
//                     if (MotionDetected)
//                     {
//                         _onPresenceDetected();
//                         WaspState = WaspState.WaspInBox;
//                     }
//                     else
//                     {
//                         _onPresenceCleared();
//                         WaspState = WaspState.NoWasp;
//                     }
//                 }
//             });
//
//         doorSensors
//             .Select(e => e.StateChanges())
//             .Merge()
//             .Subscribe(e =>
//             {
//                 if (DoorClosed)
//                 {
//                     if (MotionDetected)
//                     {
//                         _onPresenceDetected();
//                         WaspState = WaspState.WaspInBox;
//                     }
//                     else
//                     {
//                         _onPresenceCleared();
//                         WaspState = WaspState.NoWaspInBox;
//                     }
//                 }
//                 else
//                 {
//                     if (MotionDetected)
//                     {
//                         _onPresenceDetected();
//                         WaspState = WaspState.WaspInBox;
//                     }
//                     else
//                     {
//                         _onPresenceCleared();
//                         WaspState = WaspState.NoWasp;
//                     }
//                 }
//             });
//     }
// }