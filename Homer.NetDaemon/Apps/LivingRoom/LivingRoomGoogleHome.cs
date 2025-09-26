using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Homer.NetDaemon.Entities;
using NetDaemon.AppModel;
using NetDaemon.Extensions.Scheduler;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace Homer.NetDaemon.Apps.LivingRoom;

// [Focus]
[NetDaemonApp]
public class LivingRoomGoogleHome(
    MediaPlayerEntities mediaPlayerEntities,
    BinarySensorEntities binarySensorEntities,
    IScheduler scheduler
) : IAsyncInitializable
{
    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (mediaPlayerEntities.Nesthub1cef.IsOff())
        {
            mediaPlayerEntities.Nesthub1cef.PlayMedia(new MediaPlayerPlayMediaParameters
            {
                MediaContentId = "google-home",
                MediaContentType = "lovelace"
            });
        }

        binarySensorEntities.PresenceSensorFp2B4c4PresenceSensor1.StateChanges()
            .Where(s => s.Entity.IsOn())
            .Subscribe(_ =>
            {
                mediaPlayerEntities.Nesthub1cef.PlayMedia(new MediaPlayerPlayMediaParameters
                {
                    MediaContentId = "google-home",
                    MediaContentType = "lovelace"
                });
            });

        binarySensorEntities.PresenceSensorFp2B4c4PresenceSensor1.StateChanges()
            .WhenStateIsFor(s => s.IsOff(), TimeSpan.FromMinutes(3), scheduler)
            .Subscribe(_ =>
            {
                mediaPlayerEntities.Nesthub1cef.TurnOff();
                mediaPlayerEntities.Nesthub1cef.MediaStop();
            });

        return Task.CompletedTask;
    }
}