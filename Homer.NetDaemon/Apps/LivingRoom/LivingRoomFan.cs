using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Homer.NetDaemon.Entities;
using NetDaemon.AppModel;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace Homer.NetDaemon.Apps.LivingRoom;

[NetDaemonApp]
[Focus]
public class LivingRoomFan : IAsyncInitializable
{
    private readonly ILogger<LivingRoomFan> _logger;
    private readonly SensorEntities _sensorEntities;
    private readonly InputBooleanEntities _inputBooleanEntities;
    private readonly List<BinarySensorEntity> _presenceEntities;

    private bool Presence => _presenceEntities.Any(e => e.IsOn());

    public LivingRoomFan(
        ILogger<LivingRoomFan> logger,
        IScheduler scheduler,
        IrRemoteLock irRemoteLock,
        BinarySensorEntities binarySensorEntities,
        SensorEntities sensorEntities,
        InputBooleanEntities inputBooleanEntities,
        RemoteEntities remoteEntities
    )
    {
        _logger = logger;
        _sensorEntities = sensorEntities;
        _inputBooleanEntities = inputBooleanEntities;

        _presenceEntities =
        [
            binarySensorEntities.PresenceSensorFp2B4c4PresenceSensor2,
            binarySensorEntities.PresenceSensorFp2B4c4PresenceSensor3,
        ];

        var presenceObservables = _presenceEntities.Select(e => e.StateChanges()).Merge();

        presenceObservables
            .WhenStateIsFor(e =>
            {
                logger.LogDebug("Living room fan presence state changed: {State}", Presence);
                return Presence;
            }, TimeSpan.FromSeconds(1), scheduler)
            .Subscribe(_ => { TurnOn(); });

        presenceObservables
            .WhenStateIsFor(e =>
            {
                logger.LogDebug("Living room fan presence state changed: {State}", Presence);
                return !Presence;
            }, TimeSpan.FromMinutes(1), scheduler)
            .Subscribe(_ => { inputBooleanEntities.LivingRoomFan.TurnOff(); });

        _inputBooleanEntities.LivingRoomFan.StateChanges()
            .SubscribeAsync(async _ =>
            {
                await irRemoteLock.SemaphoreSlim.WaitAsync();
                await Task.Delay(1500);
                remoteEntities.LivingRoomRemote.SendCommand("Power", "Living Room KDK");
                await Task.Delay(1500);
                irRemoteLock.SemaphoreSlim.Release();
            });
    }

    private void TurnOn()
    {
        if (_sensorEntities.Daikinap16703InsideTemperature.State < 26)
        {
            _logger.LogDebug("Temperature in living room is {State}, not turning on fan",
                _sensorEntities.Daikinap16703InsideTemperature.State);
            return;
        }

        _inputBooleanEntities.LivingRoomFan.TurnOn();
    }

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (!Presence)
        {
            _inputBooleanEntities.LivingRoomFan.TurnOff();
        }

        return Task.CompletedTask;
    }
}