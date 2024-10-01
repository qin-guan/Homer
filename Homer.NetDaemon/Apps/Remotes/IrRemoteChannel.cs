using System.Threading.Channels;

namespace Homer.NetDaemon.Apps.Remotes;

public class IrRemoteChannel
{
    public Channel<LivingRoomRemoteCommand> LivingRoomChannel = Channel.CreateBounded<LivingRoomRemoteCommand>(1);
}