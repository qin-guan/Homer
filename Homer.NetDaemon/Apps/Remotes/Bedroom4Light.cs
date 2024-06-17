using Homer.NetDaemon.Entities;
using NetDaemon.AppModel;

namespace Homer.NetDaemon.Apps.Remotes;

[NetDaemonApp]
public class BedroomLight
{
    public BedroomLight(
        InputBooleanEntities inputBooleanEntities,
        RemoteEntities remoteEntities
    )
    {
        inputBooleanEntities.Bedroom4Light.StateAllChanges()
            .Subscribe((e) => { remoteEntities.Bedroom4Remote.SendCommand("Light Power", "Bedroom 4 Fanco"); });
    }
}