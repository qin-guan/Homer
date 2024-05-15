namespace Homer.NetDaemon.Apps;

public class IrRemoteLock
{
    public SemaphoreSlim SemaphoreSlim = new SemaphoreSlim(1);
}