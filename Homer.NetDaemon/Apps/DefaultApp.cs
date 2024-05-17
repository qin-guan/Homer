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
            if (e.DataElement.HasValue)
            {
                var val = e.DataElement.GetValueOrDefault();
                var tags = new List<KeyValuePair<string, object?>>();

                if (val.TryGetProperty("entity_id", out var entityId))
                {
                    tags.Add(new KeyValuePair<string, object?>("ha.entity_id", entityId.GetString()));
                }

                // if (val.TryGetProperty("new_state", out var i) &&
                //     i.TryGetProperty("attributes", out var i2) &&
                //     i2.TryGetProperty("friendly_name", out var friendlyName))
                // {
                //     tags.Add(new KeyValuePair<string, object?>("friendly_name", friendlyName.GetString()));
                // }

                eventsProcessedMeter.Add(1, tags.ToArray());
            }
            else
            {
                eventsProcessedMeter.Add(1);
            }
        });

        logger.LogInformation("Hello, home!");
    }
}