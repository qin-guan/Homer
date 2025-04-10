using Homer.NetDaemon.Entities;
using NetDaemon.AppModel;
using NetDaemon.Extensions.Scheduler;
using NetDaemon.HassModel.Entities;

namespace Homer.NetDaemon.Apps.LivingRoom;

// [Focus]
[NetDaemonApp]
public class LivingRoomGoogleHome : IAsyncInitializable
{
    private readonly MediaPlayerEntities _mediaPlayerEntities;

    public LivingRoomGoogleHome(MediaPlayerEntities mediaPlayerEntities, INetDaemonScheduler scheduler)
    {
        _mediaPlayerEntities = mediaPlayerEntities;

        scheduler.RunEvery(TimeSpan.FromMinutes(30), () =>
        {
            mediaPlayerEntities.Nesthub1cef.TurnOff();
            
            scheduler.RunIn(TimeSpan.FromMinutes(1), () =>
            {
                mediaPlayerEntities.Nesthub1cef.PlayMedia(new MediaPlayerPlayMediaParameters
                {
                    MediaContentId = "google-home",
                    MediaContentType = "lovelace"
                });
            });
        });
    }

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (_mediaPlayerEntities.Nesthub1cef.IsOff())
        {
            _mediaPlayerEntities.Nesthub1cef.PlayMedia(new MediaPlayerPlayMediaParameters
            {
                MediaContentId = "google-home",
                MediaContentType = "lovelace"
            });
        }

        return Task.CompletedTask;
    }
}