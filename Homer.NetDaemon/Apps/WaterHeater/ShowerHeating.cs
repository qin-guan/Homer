using Homer.NetDaemon.Helpers;
using Homer.NetDaemon.Services;
using NetDaemon.AppModel;
using NetDaemon.Extensions.Scheduler;
using System.Reactive.Concurrency;

namespace Homer.NetDaemon.Apps.WaterHeater;

/// <summary>
/// Decides when the tank should be heated. Showers come from the shower detection apps; every switch change still
/// goes through <see cref="WaterHeaterController"/>. Heating follows a virtual tank inventory: ride stored heat
/// during a shower, top up only if the tank would run out, then recover the deficit after a cluster of showers.
/// </summary>
[NetDaemonApp]
public sealed class ShowerHeating : IDisposable
{
    private readonly ILogger<ShowerHeating> _logger;
    private readonly BathroomStatusService _bathroomStatusService;
    private readonly WaterHeaterTimerService _waterHeaterTimerService;
    private readonly WaterHeaterInventory _inventory;
    private readonly IScheduler _scheduler;
    private readonly object _gate = new();
    private readonly List<IDisposable> _subscriptions = [];
    private IDisposable? _midShowerHeatDelay;
    private IDisposable? _clusterRecovery;
    private DateTime? _midShowerHeatAllowedAtUtc;
    private DateTime? _clusterQuietUntilUtc;
    private bool _postClusterRecoveryDue;
    private bool _policyOwnsRun;

    public ShowerHeating(
        ILogger<ShowerHeating> logger,
        BathroomStatusService bathroomStatusService,
        WaterHeaterTimerService waterHeaterTimerService,
        WaterHeaterInventory inventory,
        IScheduler scheduler)
    {
        _logger = logger;
        _bathroomStatusService = bathroomStatusService;
        _waterHeaterTimerService = waterHeaterTimerService;
        _inventory = inventory;
        _scheduler = scheduler;

        _bathroomStatusService.StatusChanged += OnBathroomStatusChanged;
        _waterHeaterTimerService.HeaterTurnedOff += OnHeaterTurnedOff;

        _subscriptions.Add(scheduler.SchedulePeriodic(0, TimeSpan.FromSeconds(30), _ =>
        {
            lock (_gate)
            {
                ReevaluateHeaterCore("periodic inventory tick");
            }
        }));

        // Typical demand windows: only pre-position heat when the tank already looks empty.
        scheduler.ScheduleCron("30 6 * * *", () => OnPreheatWindow("morning"));
        scheduler.ScheduleCron("30 18 * * *", () => OnPreheatWindow("evening"));

        lock (_gate)
        {
            ReevaluateHeaterCore("the app started");
        }
    }

    public void Dispose()
    {
        _bathroomStatusService.StatusChanged -= OnBathroomStatusChanged;
        _waterHeaterTimerService.HeaterTurnedOff -= OnHeaterTurnedOff;
        CancelMidShowerHeatDelay();
        CancelClusterRecovery();
        foreach (var subscription in _subscriptions)
        {
            subscription.Dispose();
        }
    }

    private void OnBathroomStatusChanged(BathroomStatusChange change)
    {
        lock (_gate)
        {
            if (change.Status == BathroomState.Showering)
            {
                _inventory.ApplyUndetectedShowerDraw();
                _logger.LogInformation(
                    "{Bathroom} shower confirmed. Applied {Draw:F1} minutes of undetected draw; SoC is {SoC:F1}",
                    change.Bathroom,
                    WaterHeaterInventory.UndetectedDrawMinutes,
                    _inventory.StateOfChargeMinutes);

                CancelClusterRecovery();
                _clusterQuietUntilUtc = null;
                _postClusterRecoveryDue = false;
                ScheduleMidShowerHeatDelay();
            }

            if (change.EndedShowerDuration is not null)
            {
                StartClusterQuietPeriod();
            }

            ReevaluateHeaterCore(change.Reason);
        }
    }

    private void OnHeaterTurnedOff()
    {
        lock (_gate)
        {
            _policyOwnsRun = false;
            ReevaluateHeaterCore("the water heater turned off");
        }
    }

    private void OnPreheatWindow(string window)
    {
        lock (_gate)
        {
            ReevaluateHeaterCore($"{window} preheat window opened");

            if (_waterHeaterTimerService.Controller is not { } controller)
            {
                return;
            }

            if (_bathroomStatusService.AnyShowering || _inventory.IsNightLockout(TimeHelpers.TimeNow))
            {
                return;
            }

            if (_inventory.StateOfChargeMinutes >= WaterHeaterInventory.RunoutFloorMinutes)
            {
                _logger.LogInformation(
                    "Skipping {Window} preheat because SoC is {SoC:F1}, above the runout floor of {Floor:F1}",
                    window,
                    _inventory.StateOfChargeMinutes,
                    WaterHeaterInventory.RunoutFloorMinutes);
                return;
            }

            RequestDeficitCore(
                controller,
                WaterHeaterInventory.TargetMinutes,
                $"{window} preheat and the tank inventory is below the runout floor");
        }
    }

    private void ScheduleMidShowerHeatDelay()
    {
        CancelMidShowerHeatDelay();
        _midShowerHeatAllowedAtUtc = DateTime.UtcNow.Add(WaterHeaterInventory.MidShowerHeatDelay);
        _midShowerHeatDelay = _scheduler.Schedule(
            WaterHeaterInventory.MidShowerHeatDelay,
            () =>
            {
                lock (_gate)
                {
                    _midShowerHeatDelay = null;
                    ReevaluateHeaterCore("the mid-shower heat delay elapsed");
                }
            });
    }

    private void StartClusterQuietPeriod()
    {
        CancelClusterRecovery();
        _clusterQuietUntilUtc = DateTime.UtcNow.Add(WaterHeaterInventory.ClusterGap);
        _clusterRecovery = _scheduler.Schedule(
            WaterHeaterInventory.ClusterGap,
            () =>
            {
                lock (_gate)
                {
                    _clusterRecovery = null;
                    ReevaluateHeaterCore("the shower cluster quiet period elapsed");
                }
            });
    }

    private void ReevaluateHeaterCore(string reason)
    {
        if (_waterHeaterTimerService.Controller is not { } controller)
        {
            return;
        }

        var now = DateTime.UtcNow;
        _inventory.Tick(now, controller.IsHeaterOn, _bathroomStatusService.AnyShowering);

        if (_bathroomStatusService.AnyShowering)
        {
            HandleActiveShowerCore(controller, now, reason);
            return;
        }

        _midShowerHeatAllowedAtUtc = null;

        if (_clusterQuietUntilUtc is { } quietUntil && quietUntil <= now)
        {
            _clusterQuietUntilUtc = null;
            _postClusterRecoveryDue = true;
        }

        if (_clusterQuietUntilUtc is not null)
        {
            // Another shower may still arrive. Only heat if the tank would run out before then.
            if (_inventory.StateOfChargeMinutes < WaterHeaterInventory.RunoutFloorMinutes)
            {
                RequestDeficitCore(
                    controller,
                    WaterHeaterInventory.RunoutFloorMinutes + WaterHeaterController.MinimumHeaterRunDuration.TotalMinutes,
                    $"{reason} and SoC is below the runout floor during the cluster quiet period");
                return;
            }

            ReleaseIfPolicyRunReachedTarget(controller, reason);
            return;
        }

        if (_inventory.IsNightLockout(TimeHelpers.TimeNow))
        {
            _postClusterRecoveryDue = false;
            if (_policyOwnsRun && controller.IsHeaterOn)
            {
                controller.ReleaseHeating($"{reason} and night lockout is active");
            }

            return;
        }

        if (_postClusterRecoveryDue)
        {
            _postClusterRecoveryDue = false;
            if (_inventory.StateOfChargeMinutes < WaterHeaterInventory.TargetMinutes &&
                WaterHeaterInventory.RecoveryDuration(_inventory.StateOfChargeMinutes) >=
                WaterHeaterController.MinimumHeaterRunDuration)
            {
                RequestDeficitCore(
                    controller,
                    WaterHeaterInventory.TargetMinutes,
                    $"{reason} and post-cluster recovery is due");
                return;
            }
        }

        ReleaseIfPolicyRunReachedTarget(controller, reason);
    }

    private void HandleActiveShowerCore(IWaterHeaterController controller, DateTime now, string reason)
    {
        if (_policyOwnsRun &&
            _inventory.StateOfChargeMinutes >= WaterHeaterInventory.CapacityMinutes &&
            controller.IsHeaterOn)
        {
            controller.ReleaseHeating($"{reason} and the estimated tank inventory is full");
            return;
        }

        var delayElapsed = _midShowerHeatAllowedAtUtc is null || now >= _midShowerHeatAllowedAtUtc;
        if (_inventory.StateOfChargeMinutes < WaterHeaterInventory.RunoutFloorMinutes && delayElapsed)
        {
            RequestDeficitCore(
                controller,
                WaterHeaterInventory.RunoutFloorMinutes + WaterHeaterController.MinimumHeaterRunDuration.TotalMinutes,
                $"{reason} and a shower is active with SoC below the runout floor");
            return;
        }

        ReleaseIfPolicyRunReachedTarget(controller, reason);
    }

    private void ReleaseIfPolicyRunReachedTarget(IWaterHeaterController controller, string reason)
    {
        if (_policyOwnsRun &&
            controller.IsHeaterOn &&
            _inventory.StateOfChargeMinutes >= WaterHeaterInventory.TargetMinutes)
        {
            controller.ReleaseHeating($"{reason} and the tank is already at the target inventory");
        }
    }

    private void RequestDeficitCore(IWaterHeaterController controller, double targetMinutes, string reason)
    {
        var duration = WaterHeaterInventory.RecoveryDuration(_inventory.StateOfChargeMinutes, targetMinutes);
        if (duration <= TimeSpan.Zero)
        {
            return;
        }

        if (duration < WaterHeaterController.MinimumHeaterRunDuration &&
            _inventory.StateOfChargeMinutes >= WaterHeaterInventory.RunoutFloorMinutes)
        {
            _logger.LogInformation(
                "Skipping heat for {Reason} because the deficit is only {Minutes:F1} minutes (SoC {SoC:F1})",
                reason,
                duration.TotalMinutes,
                _inventory.StateOfChargeMinutes);
            return;
        }

        _logger.LogInformation(
            "Requesting {Minutes:F1} minutes of heat because {Reason}. SoC is {SoC:F1}",
            duration.TotalMinutes,
            reason,
            _inventory.StateOfChargeMinutes);

        _policyOwnsRun = true;
        controller.RequestHeating(reason, duration);
    }

    private void CancelMidShowerHeatDelay()
    {
        _midShowerHeatDelay?.Dispose();
        _midShowerHeatDelay = null;
    }

    private void CancelClusterRecovery()
    {
        _clusterRecovery?.Dispose();
        _clusterRecovery = null;
    }
}
