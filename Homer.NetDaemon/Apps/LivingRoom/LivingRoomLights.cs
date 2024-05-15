using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Homer.NetDaemon.Entities;
using NetDaemon.AppModel;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace Homer.NetDaemon.Apps.LivingRoom;

[NetDaemonApp]
public class LivingRoomLights: IAsyncInitializable
{
    private readonly SensorEntities _sensorEntities;
    private readonly InputBooleanEntities _inputBooleanEntities;
    private readonly List<BinarySensorEntity> _presenceEntities;

    public LivingRoomLights(
        ILogger<LivingRoomLights> logger,
        IScheduler scheduler,
        IrRemoteLock irRemoteLock,
        BinarySensorEntities binarySensorEntities,
        SensorEntities sensorEntities,
        InputBooleanEntities inputBooleanEntities,
        RemoteEntities remoteEntities
    )
    {
        _sensorEntities = sensorEntities;
        _inputBooleanEntities = inputBooleanEntities;

        _presenceEntities = new List<BinarySensorEntity>
        {
            binarySensorEntities.PresenceSensorFp2B4c4PresenceSensor2,
            binarySensorEntities.PresenceSensorFp2B4c4PresenceSensor3,
            binarySensorEntities.PresenceSensorFp2B4c4PresenceSensor4,
            binarySensorEntities.PresenceSensorFp2B4c4PresenceSensor5,
            binarySensorEntities.PresenceSensorFp2B4c4PresenceSensor7,
            binarySensorEntities.PresenceSensorFp2B4c4PresenceSensor8,
        };

        var presenceObservables = _presenceEntities.Select(e => e.StateChanges()).Merge();
        
        presenceObservables
            .Where(e =>
            {
                logger.LogDebug("Living room presence state changed: {State}", e.New);
                return e.New.IsOn();
            })
            .Subscribe(_ => { TurnOn(); });

        presenceObservables
            .WhenStateIsFor(e =>
            {
                logger.LogDebug("Living room presence state changed: {State}", e?.State);
                return e.IsOff() && _presenceEntities.All(entity => entity.IsOff());
            }, TimeSpan.FromMinutes(1), scheduler)
            .Subscribe(_ => { inputBooleanEntities.LivingRoomFanLights.TurnOff(); });
        
        inputBooleanEntities.LivingRoomFanLights.StateChanges()
            .SubscribeAsync(async _ =>
            {
                await irRemoteLock.SemaphoreSlim.WaitAsync();
                await Task.Delay(1000);
                remoteEntities.LivingRoomRemote.SendCommand("Light Power", "Living Room KDK");
                await Task.Delay(1000);
                irRemoteLock.SemaphoreSlim.Release();
            });
    }

    private void TurnOn()
    {
        if (_sensorEntities.PresenceSensorFp2B4c4LightSensorLightLevel.State > 60) return;
        _inputBooleanEntities.LivingRoomFanLights.TurnOn();
    }

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (_presenceEntities.All(e => e.IsOff()))
        {
            _inputBooleanEntities.LivingRoomFanLights.TurnOff();
        }

        return Task.CompletedTask;
    }
}