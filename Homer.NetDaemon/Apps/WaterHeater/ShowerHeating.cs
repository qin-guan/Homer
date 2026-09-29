using Homer.NetDaemon.Services;
using NetDaemon.AppModel;

namespace Homer.NetDaemon.Apps.WaterHeater;

/// <summary>
/// Heats water while anyone is showering, and for a while after long showers so the tank recovers. Showers come from
/// the shower detection apps; heating goes through <see cref="WaterHeaterController"/>.
/// </summary>
[NetDaemonApp]
public sealed class ShowerHeating : IDisposable
{
    private static readonly TimeSpan RecoveryShowerDurationThreshold = TimeSpan.FromMinutes(5);
    private const double PostShowerRecoveryMultiplier = 1.4;

    private readonly ILogger<ShowerHeating> _logger;
    private readonly BathroomStatusService _bathroomStatusService;
    private readonly WaterHeaterTimerService _waterHeaterTimerService;
    private readonly object _gate = new();
    private DateTime? _postShowerRecoveryUntilUtc;

    public ShowerHeating(
        ILogger<ShowerHeating> logger,
        BathroomStatusService bathroomStatusService,
        WaterHeaterTimerService waterHeaterTimerService)
    {
        _logger = logger;
        _bathroomStatusService = bathroomStatusService;
        _waterHeaterTimerService = waterHeaterTimerService;

        _bathroomStatusService.StatusChanged += OnBathroomStatusChanged;
        _waterHeaterTimerService.HeaterTurnedOff += OnHeaterTurnedOff;

        lock (_gate)
        {
            ReevaluateHeaterCore("the app started");
        }
    }

    public void Dispose()
    {
        _bathroomStatusService.StatusChanged -= OnBathroomStatusChanged;
        _waterHeaterTimerService.HeaterTurnedOff -= OnHeaterTurnedOff;
    }

    private void OnBathroomStatusChanged(BathroomStatusChange change)
    {
        lock (_gate)
        {
            if (change.EndedShowerDuration is { } showerDuration)
            {
                StartPostShowerRecoveryCore(change.Bathroom, showerDuration);
            }

            ReevaluateHeaterCore(change.Reason);
        }
    }

    private void OnHeaterTurnedOff()
    {
        lock (_gate)
        {
            ReevaluateHeaterCore("the water heater turned off");
        }
    }

    private void StartPostShowerRecoveryCore(BathroomId bathroom, TimeSpan showerDuration)
    {
        var now = DateTime.UtcNow;
        var recoveryDuration = showerDuration > RecoveryShowerDurationThreshold
            ? TimeSpan.FromMinutes(Math.Max(0, showerDuration.TotalMinutes * PostShowerRecoveryMultiplier))
            : TimeSpan.Zero;
        var recoveryUntil = now.Add(recoveryDuration);

        _logger.LogInformation(
            "{Bathroom} shower lasted {ShowerMinutes:F1} minutes. Keeping the water heater available for {RecoveryMinutes:F1} recovery minutes",
            bathroom,
            showerDuration.TotalMinutes,
            recoveryDuration.TotalMinutes);

        if (recoveryDuration > TimeSpan.Zero &&
            (_postShowerRecoveryUntilUtc is null || recoveryUntil > _postShowerRecoveryUntilUtc))
        {
            _postShowerRecoveryUntilUtc = recoveryUntil;
        }
    }

    private void ReevaluateHeaterCore(string reason)
    {
        // Heating is only possible through the controller, so there is nothing to do while it is disabled.
        if (_waterHeaterTimerService.Controller is not { } controller)
        {
            return;
        }

        // Active showers and post-shower recovery are the only reasons the heater may stay on.
        if (_bathroomStatusService.AnyShowering)
        {
            controller.RequestHeating($"{reason} and a shower is active", WaterHeaterController.MaxHeaterRunDuration);
            return;
        }

        var now = DateTime.UtcNow;
        if (_postShowerRecoveryUntilUtc <= now)
        {
            _postShowerRecoveryUntilUtc = null;
        }

        if (_postShowerRecoveryUntilUtc is { } recoveryUntil)
        {
            var remainingRecovery = recoveryUntil - now;
            if (!controller.IsHeaterOn && remainingRecovery < WaterHeaterController.MinimumHeaterRunDuration)
            {
                _logger.LogInformation(
                    "Skipping post-shower recovery because only {Minutes:F1} minutes remain, below the minimum heater run duration of {MinimumMinutes:F1} minutes",
                    remainingRecovery.TotalMinutes,
                    WaterHeaterController.MinimumHeaterRunDuration.TotalMinutes);
                _postShowerRecoveryUntilUtc = null;
            }
            else
            {
                controller.RequestHeating(
                    $"{reason} and post-shower recovery is active",
                    remainingRecovery);
                return;
            }
        }

        controller.ReleaseHeating($"{reason} and no shower or recovery is active");
    }
}
