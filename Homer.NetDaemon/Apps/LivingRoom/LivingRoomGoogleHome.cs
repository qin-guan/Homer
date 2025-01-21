using System.Reactive.Concurrency;
using Homer.NetDaemon.Entities;
using NetDaemon.AppModel;
using NetDaemon.Extensions.Scheduler;

namespace Homer.NetDaemon.Apps.LivingRoom;

[NetDaemonApp]
public class LivingRoomGoogleHome
{
    public LivingRoomGoogleHome(MediaPlayerEntities mediaPlayerEntities, IScheduler scheduler)
    {
        scheduler.ScheduleCron("0 * * * *", () =>
        {
            mediaPlayerEntities.Home.PlayMedia(new MediaPlayerPlayMediaParameters
            {
                MediaContentId = "google-home",
                MediaContentType = "lovelace"
            });
        });
        
        scheduler.ScheduleCron("55 */2 * * *", () =>
        {
            mediaPlayerEntities.Home.MediaStop();
        });
    }
}