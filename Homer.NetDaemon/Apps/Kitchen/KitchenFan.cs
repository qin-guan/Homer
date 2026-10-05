using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Homer.NetDaemon.Entities;
using Homer.ServiceDefaults.Metrics;
using NetDaemon.AppModel;
using NetDaemon.HassModel.Entities;

namespace Homer.NetDaemon.Apps.Kitchen;

[NetDaemonApp]
public class KitchenFan : IAsyncInitializable
{
    private readonly ILogger<KitchenFan> _logger;
    private readonly FanEntity _fan;
    private readonly List<BinarySensorEntity> _presenceEntities;

    private bool Presence => _presenceEntities.Any(e => e.IsOn());

    public KitchenFan(
        ILogger<KitchenFan> logger,
        IScheduler scheduler,
        BinarySensorEntities binarySensorEntities,
        FanEntities fanEntities
    )
    {

        _logger = logger;
        _fan = fanEntities.DmakerSg4682300181cS2Fan;

        List<BinarySensorEntity> triggerEntities = [
            binarySensorEntities.PresenceSensorFp2B4c4PresenceSensor6,
            binarySensorEntities.ScreekHumanSensor2a872668Zone1Presence
        ];

        _presenceEntities =
        [
            binarySensorEntities.PresenceSensorFp2B4c4PresenceSensor6,
            binarySensorEntities.ScreekHumanSensor2a872668Zone1Presence
        ];

        var triggerObservables = triggerEntities.Select(e => e.StateChanges()).Merge();
        var presenceObservables = _presenceEntities.Select(e => e.StateChanges()).Merge();

        triggerObservables
            .Where(_ =>
            {
                EntityMetrics.AutomationEvent("kitchen_fan");
                return triggerEntities.Any(e => e.IsOn());
            })
            .Subscribe(_ =>
            {
                _fan.TurnOn();
            });

        presenceObservables
            .Where(_ =>
            {
                EntityMetrics.AutomationEvent("kitchen_fan");
                return !Presence;
            })
            .Throttle(TimeSpan.FromMinutes(12), scheduler)
            .Where(_ => !Presence)
            .Subscribe(_ => { _fan.TurnOff(); });
    }

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (!Presence)
        {
            _fan.TurnOff();
        }

        return Task.CompletedTask;
    }
}
