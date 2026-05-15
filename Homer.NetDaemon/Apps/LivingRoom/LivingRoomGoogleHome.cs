using System.Reactive.Concurrency;
using Homer.NetDaemon.Entities;
using NetDaemon.AppModel;

namespace Homer.NetDaemon.Apps.LivingRoom;

[NetDaemonApp]
public class LivingRoomGoogleHome(
    MediaPlayerEntities mediaPlayerEntities,
    BinarySensorEntities binarySensorEntities,
    IScheduler scheduler
) : IAsyncInitializable
{
    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        mediaPlayerEntities.Nesthub1cef.PlayMedia(new MediaPlayerPlayMediaParameters
        {
            Media = new
            {
                media_content_id = "google-home",
                media_content_type = "lovelace"
            }
        });

        binarySensorEntities.PresenceSensorFp2B4c4PresenceSensor1.StateChanges()
            .Subscribe(_ =>
            {
                mediaPlayerEntities.Nesthub1cef.PlayMedia(new MediaPlayerPlayMediaParameters
                {
                    Media = new
                    {
                        media_content_id = "google-home",
                        media_content_type = "lovelace"
                    }
                });
            });

        return Task.CompletedTask;
    }
}