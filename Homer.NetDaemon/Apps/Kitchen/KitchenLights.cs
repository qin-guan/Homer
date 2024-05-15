// using System.Reactive.Concurrency;
// using System.Reactive.Linq;
// using Homer.NetDaemon.Entities;
// using NetDaemon.AppModel;
// using NetDaemon.HassModel;
// using NetDaemon.HassModel.Entities;
//
// namespace Homer.NetDaemon.Apps.Kitchen;
//
// [NetDaemonApp]
// [Focus]
// public class KitchenLights
// {
//     public KitchenLights(
//         ILogger<KitchenLights> logger,
//         IScheduler scheduler,
//         BinarySensorEntities binarySensorEntities,
//         SwitchEntities switchEntities,
//         SensorEntities sensors
//     )
//     {
//         binarySensorEntities.KitchenTuyaPresencePresence.StateChanges()
//             .WhenStateIsFor(
//                 e => e.IsOff(),
//                 TimeSpan.FromSeconds(5),
//                 scheduler
//             )
//             .Subscribe(_ =>
//             {
//                 switchEntities.KitchenLightsLeft.TurnOff();
//                 switchEntities.KitchenLightsRight.TurnOff();
//             });
//
//         binarySensorEntities.KitchenTuyaPresencePresence.StateChanges()
//             .Where(e => e.New.IsOn())
//             .Subscribe(e =>
//             {
//                 if (
//                     TimeOnly.FromDateTime(DateTime.Now) > new TimeOnly(22, 0)
//                     && TimeOnly.FromDateTime(DateTime.Now) < new TimeOnly(6, 0)
//                 )
//                     switchEntities.KitchenLightsLeft.TurnOn();
//                 else
//                 {
//                     if (sensors.PresenceSensorFp2B4c4LightSensorLightLevel.State < 60)
//                     {
//                         switchEntities.KitchenLightsRight.TurnOn();
//                     }
//                 }
//             });
//     }
// }