namespace Homer.NetDaemon.Apps.Remotes;

public class IrRemoteLock
{
    public SemaphoreSlim LivingRoom = new(1);
    public SemaphoreSlim MasterBedroom = new(1);
}