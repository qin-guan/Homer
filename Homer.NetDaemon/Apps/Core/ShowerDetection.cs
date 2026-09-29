using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Homer.NetDaemon.Entities;
using Homer.NetDaemon.Services;
using NetDaemon.HassModel.Entities;

namespace Homer.NetDaemon.Apps.Core;

/// <summary>
/// Detects showers in a bathroom and publishes its status to <see cref="BathroomStatusService"/>, which the dashboard
/// and shower heating read. Presence without motion for a while means someone is standing still in the shower.
/// </summary>
public abstract class ShowerDetection : IDisposable
{
    private static readonly TimeSpan ShowerDetectionConfirmationDelay = TimeSpan.FromMinutes(4);

    private readonly ILogger _logger;
    private readonly IScheduler _scheduler;
    private readonly BathroomStatusService _bathroomStatusService;
    private readonly BathroomId _bathroom;
    private readonly InputBooleanEntity _presence;
    private readonly BinarySensorEntity[] _motionSensors;
    private readonly object _gate = new();
    private IDisposable? _showerConfirmation;
    private DateTime? _showerStartTimeUtc;

    private bool IsShoweringDetected => _showerStartTimeUtc is not null;

    protected ShowerDetection(
        ILogger logger,
        IScheduler scheduler,
        BathroomStatusService bathroomStatusService,
        BathroomId bathroom,
        InputBooleanEntity presence,
        BinarySensorEntity[] motionSensors)
    {
        _logger = logger;
        _scheduler = scheduler;
        _bathroomStatusService = bathroomStatusService;
        _bathroom = bathroom;
        _presence = presence;
        _motionSensors = motionSensors;

        Observable.Merge(motionSensors.Select(m => m.StateChanges()))
            .Subscribe(e =>
            {
                lock (_gate)
                {
                    if (IsShoweringDetected && e.New.IsOn())
                    {
                        OnShowerEndedCore($"{_bathroom} motion was detected after the shower started");
                        return;
                    }

                    EvaluateShowerStateCore();
                }
            });

        presence.StateChanges().Subscribe(_ =>
        {
            lock (_gate)
            {
                if (presence.IsOff())
                {
                    CancelShowerConfirmationCore();

                    if (IsShoweringDetected)
                    {
                        OnShowerEndedCore($"{_bathroom} became unoccupied");
                    }
                    else
                    {
                        PublishStatusCore($"{_bathroom} became unoccupied");
                    }

                    return;
                }

                EvaluateShowerStateCore();
                PublishStatusCore($"{_bathroom} became occupied");
            }
        });

        lock (_gate)
        {
            EvaluateShowerStateCore();
            PublishStatusCore($"{_bathroom} shower detection started");
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            CancelShowerConfirmationCore();

            // Don't leave a stale "showering" status behind, or shower heating would keep reheating for it.
            _showerStartTimeUtc = null;
            PublishStatusCore($"{_bathroom} shower detection stopped");
        }
    }

    private void EvaluateShowerStateCore()
    {
        if (IsShoweringDetected)
        {
            return;
        }

        var showerCandidate = _presence.IsOn() && _motionSensors.All(m => m.IsOff());
        // Presence without motion can mean the person is standing still in the shower.
        if (!showerCandidate)
        {
            CancelShowerConfirmationCore();
            return;
        }

        if (_showerConfirmation is not null)
        {
            return;
        }

        _logger.LogInformation(
            "{Bathroom} has presence with no shower motion. Confirming shower state in {Delay:g}",
            _bathroom,
            ShowerDetectionConfirmationDelay);

        _showerConfirmation = _scheduler.Schedule(ShowerDetectionConfirmationDelay, () =>
        {
            lock (_gate)
            {
                _showerConfirmation = null;

                if (IsShoweringDetected ||
                    _presence.IsOff() ||
                    _motionSensors.Any(m => m.IsOn()))
                {
                    PublishStatusCore($"{_bathroom} shower confirmation was cancelled");
                    return;
                }

                OnShowerStartedCore();
            }
        });
    }

    private void OnShowerStartedCore()
    {
        _logger.LogInformation(
            "{Bathroom} shower confirmed after {Delay:g}",
            _bathroom,
            ShowerDetectionConfirmationDelay);

        _showerStartTimeUtc = DateTime.UtcNow;
        PublishStatusCore($"{_bathroom} showering was detected");
    }

    private void OnShowerEndedCore(string reason)
    {
        var showerDuration = _showerStartTimeUtc is { } showerStartTimeUtc
            ? DateTime.UtcNow - showerStartTimeUtc
            : TimeSpan.Zero;

        _logger.LogInformation(
            "{Bathroom} shower ended after {ShowerMinutes:F1} minutes. Reason: {Reason}",
            _bathroom,
            showerDuration.TotalMinutes,
            reason);

        CancelShowerConfirmationCore();
        _showerStartTimeUtc = null;
        PublishStatusCore($"{_bathroom} shower ended", showerDuration);
    }

    private void PublishStatusCore(string reason, TimeSpan? endedShowerDuration = null)
    {
        var status = IsShoweringDetected ? BathroomState.Showering
            : _presence.IsOn() ? BathroomState.Occupied
            : BathroomState.Unoccupied;

        _bathroomStatusService.SetStatus(new BathroomStatusChange(_bathroom, status, reason, endedShowerDuration));
    }

    private void CancelShowerConfirmationCore()
    {
        _showerConfirmation?.Dispose();
        _showerConfirmation = null;
    }
}
