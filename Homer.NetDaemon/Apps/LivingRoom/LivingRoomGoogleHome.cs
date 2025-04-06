using System.Reactive.Concurrency;
using Homer.NetDaemon.Entities;
using NetDaemon.AppModel;
using NetDaemon.Extensions.Scheduler;

namespace Homer.NetDaemon.Apps.LivingRoom;

// [Focus]
[NetDaemonApp]
public class LivingRoomGoogleHome
{
    public LivingRoomGoogleHome(MediaPlayerEntities mediaPlayerEntities, IScheduler scheduler)
    {
        scheduler.SchedulePeriodic(TimeSpan.FromMinutes(5), () =>
        {
            mediaPlayerEntities.Nesthub1cef.PlayMedia(new MediaPlayerPlayMediaParameters
            {
                MediaContentId = "google-home",
                MediaContentType = "lovelace"
            });
        });

        scheduler.ScheduleCron("55 */2 * * *", () => { mediaPlayerEntities.Nesthub1cef.TurnOff(); });
    }
}