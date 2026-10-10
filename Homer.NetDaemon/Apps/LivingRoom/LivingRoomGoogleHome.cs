using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Homer.NetDaemon.Entities;
using NetDaemon.AppModel;
using NetDaemon.HassModel.Entities;

namespace Homer.NetDaemon.Apps.LivingRoom;

/// <summary>
/// Keeps the Home Assistant dashboard on the living room Nest Hub while someone is in the living room. The hub closes
/// the cast on its own after a while and falls back to its photo frame; casting only when presence changed missed that
/// whenever presence was already on, and the photo frame stayed up. Instead the dashboard is cast whenever someone is
/// around and the hub sits idle: checked when presence or the hub changes, and every minute in case either change was
/// missed or a cast didn't take.
/// </summary>
[NetDaemonApp]
public sealed class LivingRoomGoogleHome : IDisposable
{
    /// <summary>The Home Assistant Cast receiver, pychromecast's APP_HOMEASSISTANT_LOVELACE.</summary>
    public const string DashboardAppId = "A078F6B0";

    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);

    /// <summary>The receiver takes a few seconds to launch, during which the hub still reports idle.</summary>
    private static readonly TimeSpan CastCooldown = TimeSpan.FromSeconds(45);

    private readonly ILogger<LivingRoomGoogleHome> _logger;
    private readonly IScheduler _scheduler;
    private readonly MediaPlayerEntity _hub;
    private readonly BinarySensorEntity[] _presence;
    private readonly IDisposable _subscription;
    private DateTimeOffset? _lastCast;

    public LivingRoomGoogleHome(
        ILogger<LivingRoomGoogleHome> logger,
        IScheduler scheduler,
        MediaPlayerEntities mediaPlayers,
        BinarySensorEntities binarySensors)
    {
        _logger = logger;
        _scheduler = scheduler;
        _hub = mediaPlayers.Nesthub1cef;

        // The FP2's general zone covers the whole open area; the living room zones also catch someone walking in
        // while it is already on.
        _presence =
        [
            binarySensors.PresenceSensorFp2B4c4PresenceSensor1, binarySensors.PresenceSensorFp2B4c4PresenceSensor2,
            binarySensors.PresenceSensorFp2B4c4PresenceSensor3, binarySensors.PresenceSensorFp2B4c4PresenceSensor4,
            binarySensors.PresenceSensorFp2B4c4PresenceSensor7
        ];

        // Merge serialises the sources, so checks never overlap.
        _subscription = Observable.Merge(
                Observable.Timer(TimeSpan.Zero, CheckInterval, scheduler).Select(_ => Unit.Default),
                _presence.Select(p => p.StateChanges()).Merge().Where(e => e.New.IsOn()).Select(_ => Unit.Default),
                _hub.StateChanges().Select(_ => Unit.Default))
            .Subscribe(_ => CastIfIdle());
    }

    public void Dispose() => _subscription.Dispose();

    /// <summary>
    /// Whether the hub is free for the dashboard: Home Assistant reports "off" while it shows its photo frame or clock,
    /// and anything else cast to it reports a state of its own, so music or a video is never interrupted.
    /// </summary>
    public static bool ShouldCast(bool occupied, string? hubState, string? appId) =>
        occupied && hubState == "off" && appId != DashboardAppId;

    private void CastIfIdle()
    {
        try
        {
            var appId = _hub.Attributes?.AppId;
            if (!ShouldCast(_presence.Any(p => p.IsOn()), _hub.State, appId))
            {
                return;
            }

            var now = _scheduler.Now;
            if (now - _lastCast < CastCooldown)
            {
                return;
            }

            _lastCast = now;
            _logger.LogInformation("Casting the dashboard to the living room Nest Hub, which was running {AppId}",
                appId ?? "nothing");

            _hub.PlayMedia(new MediaPlayerPlayMediaParameters
            {
                Media = new
                {
                    media_content_id = "google-home",
                    media_content_type = "lovelace"
                }
            });
        }
        catch (Exception e)
        {
            // Keep watching: the next change or check retries.
            _logger.LogWarning(e, "Failed to cast the dashboard to the living room Nest Hub");
        }
    }
}
