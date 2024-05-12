using NetDaemon.AppModel;

namespace Homer.NetDaemon.Apps;

[NetDaemonApp]
public class DefaultApp
{
    public DefaultApp(ILogger<DefaultApp> logger)
    {
        logger.LogInformation("Hello, home!");
    }
}