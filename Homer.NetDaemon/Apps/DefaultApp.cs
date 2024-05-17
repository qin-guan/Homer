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
                var val = e.DataElement.GetValueOrDefault();
                
                var entityId = val.GetProperty("entity_id").GetString();
                var friendlyName = val
                    .GetProperty("new_state")
                    .GetProperty("attributes")
                    .GetProperty("friendly_name")
                    .GetString();

                eventsProcessedMeter.Add(1, [
                    new KeyValuePair<string, object?>("entity_id", entityId),
                    new KeyValuePair<string, object?>("friendly_name", friendlyName)
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