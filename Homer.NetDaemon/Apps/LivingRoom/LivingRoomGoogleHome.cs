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
/// around and the hub has sat idle for a few minutes: checked when presence changes, and every minute in case a change
/// was missed or a cast didn't take.
/// <para>
/// The hub's own screens (home controls, timers, the photo frame woken by a tap) read as idle to Home Assistant just
/// like the photo frame, so casting the moment the hub went idle took the screen back from whoever was tapping it.
/// Waiting for the hub to sit idle leaves it to them; someone walking in after a quiet spell still gets the dashboard
/// at once, since the hub has been idle all along.
/// </para>
/// </summary>
[NetDaemonApp]
public sealed class LivingRoomGoogleHome : IDisposable
{
    /// <summary>The Home Assistant Cast receiver, pychromecast's APP_HOMEASSISTANT_LOVELACE.</summary>
    public const string DashboardAppId = "A078F6B0";

    /// <summary>How long the hub must have been idle, after the dashboard or anything else left it, before recasting.</summary>
    public static readonly TimeSpan IdleBeforeCast = TimeSpan.FromMinutes(5);

    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);

    /// <summary>The receiver takes a few seconds to launch, during which the hub still reports idle.</summary>
    private static readonly TimeSpan CastCooldown = TimeSpan.FromSeconds(45);

    private readonly ILogger<LivingRoomGoogleHome> _logger;
    private readonly IScheduler _scheduler;
    private readonly MediaPlayerEntity _hub;
    private readonly BinarySensorEntity[] _presence;
    private readonly IDisposable _subscription;
    private readonly IDisposable _departures;
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

        // Merge serialises the sources, so checks never overlap. The hub's own changes aren't a trigger: a hub that
        // has just changed hasn't been idle long enough to cast to.
        _subscription = Observable.Merge(
                Observable.Timer(TimeSpan.Zero, CheckInterval, scheduler).Select(_ => Unit.Default),
                _presence.Select(p => p.StateChanges()).Merge().Where(e => e.New.IsOn()).Select(_ => Unit.Default))
            .Subscribe(_ => CastIfIdle());

        // Logged so a dashboard that drops out while being used can be told apart from one that times out.
        _departures = _hub.StateChanges()
            .Where(e => e.Old?.State == "playing" && e.Old.Attributes?.AppId == DashboardAppId)
            .Subscribe(e => _logger.LogInformation(
                "The dashboard left the living room Nest Hub, which is now {State}; it is recast once the hub has " +
                "been idle for {IdleBeforeCast}", e.New?.State, IdleBeforeCast));
    }

    public void Dispose()
    {
        _subscription.Dispose();
        _departures.Dispose();
    }

    /// <summary>
    /// Whether the hub is free for the dashboard: Home Assistant reports "off" while it shows its photo frame or its own
    /// screens, and anything else cast to it reports a state of its own, so music or a video is never interrupted.
    /// </summary>
    /// <param name="idleFor">How long the hub has been in its current state.</param>
    public static bool ShouldCast(bool occupied, string? hubState, string? appId, TimeSpan idleFor) =>
        occupied && hubState == "off" && appId != DashboardAppId && idleFor >= IdleBeforeCast;

    private void CastIfIdle()
    {
        try
        {
            var now = _scheduler.Now;
            var appId = _hub.Attributes?.AppId;
            var idleFor = _hub.EntityState?.LastChanged is { } changed
                ? now.UtcDateTime - changed.ToUniversalTime()
                : TimeSpan.MaxValue;
            if (!ShouldCast(_presence.Any(p => p.IsOn()), _hub.State, appId, idleFor))
            {
                return;
            }

            if (now - _lastCast < CastCooldown)
            {
                return;
            }

            _lastCast = now;
            _logger.LogInformation("Casting the dashboard to the living room Nest Hub, which was idle for {IdleMinutes} minutes",
                (long)idleFor.TotalMinutes);

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
