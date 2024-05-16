namespace Homer.NetDaemon.Apps.Remotes;

public class IrRemoteLock
{
    public SemaphoreSlim SemaphoreSlim = new SemaphoreSlim(1);
}