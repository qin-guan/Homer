using Homer.NetDaemon.Entities;
using NetDaemon.AppModel;

namespace Homer.NetDaemon.Apps.Remotes;

[NetDaemonApp]
public class Bedroom4Light
{
    public Bedroom4Light(
        InputBooleanEntities inputBooleanEntities,
        RemoteEntities remoteEntities
    )
    {
        inputBooleanEntities.Bedroom4Light.StateAllChanges()
            .Subscribe((e) => { remoteEntities.Bedroom4Remote.SendCommand("Light Power", "Bedroom 4 Fanco"); });
    }
}