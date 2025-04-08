using System.Threading.Channels;
using Homer.NetDaemon.Apps.Remotes;

namespace Homer.NetDaemon.Channels;

public class Channels
{
    public static Channel<TimeSpan> TurnOffWaterHeaterSwitch = Channel.CreateUnbounded<TimeSpan>();

    public static Channel<LivingRoomRemoteCommand> LivingRoomChannel = Channel.CreateBounded<LivingRoomRemoteCommand>(
        new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.Wait
        }
    );
}