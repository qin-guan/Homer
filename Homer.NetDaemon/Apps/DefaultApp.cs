using Homer.ServiceDefaults.Metrics;
using NetDaemon.AppModel;
using NetDaemon.HassModel;

namespace Homer.NetDaemon.Apps;

[Focus]
[NetDaemonApp]
public class DefaultApp
{
    public DefaultApp(ILogger<DefaultApp> logger, IHaContext haContext)
    {
        var eventsProcessedMeter =
            EntityMetrics.MeterInstance.CreateCounter<int>("homer.netdaemon.homeassistant.events_processed");

        haContext.Events.Subscribe(e =>
        {
            if (e.DataElement is not null)
            {
                eventsProcessedMeter.Add(1, [
                    new KeyValuePair<string, object?>("entity_id",
                        e.DataElement.Value.GetProperty("entity_id").GetString()),
                    new KeyValuePair<string, object?>("friendly_name",
                        e.DataElement.Value.GetProperty("new_state")
                            .GetProperty("attributes")
                            .GetProperty("friendly_name")
                            .GetString())
                ]);
            }
            else
            {
                eventsProcessedMeter.Add(1);
            }
        });

        logger.LogInformation("Hello, home!");
    }
}