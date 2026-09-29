using System.Threading.Channels;

namespace Homer.NetDaemon.Channels;

public enum BalconyBlindAction
{
    Up,
    Down,
    Stop,
    Calibrate,
    GoToPosition
}

public record BalconyBlindCommand(BalconyBlindAction Action, List<int> Blinds, double? Position = null);

public static class BalconyBlindsChannel
{
    public static Channel<BalconyBlindCommand> Channel = System.Threading.Channels.Channel.CreateUnbounded<BalconyBlindCommand>();

    /// <summary>Lowers every blind all the way.</summary>
    public static void CloseAll() =>
        Channel.Writer.TryWrite(new BalconyBlindCommand(BalconyBlindAction.GoToPosition, [0, 1, 2], 3.0));
}
