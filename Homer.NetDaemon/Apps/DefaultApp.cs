using System.Diagnostics;
using System.Reactive.Concurrency;
using System.Text.Json;
using Homer.ServiceDefaults.Metrics;
using NetDaemon.AppModel;
using NetDaemon.Extensions.MqttEntityManager;
using NetDaemon.HassModel;

namespace Homer.NetDaemon.Apps;

[Focus]
[NetDaemonApp]
public class DefaultApp
{
    private readonly ActivitySource _activitySource = new("Homer.NetDaemon.Apps.DefaultApp");

    public DefaultApp(ILogger<DefaultApp> logger, IHaContext haContext, IScheduler scheduler, IMqttEntityManager manager)
    {
        manager.RemoveAsync("climate.water_heater_2");
        manager.RemoveAsync("climate.10");
        manager.RemoveAsync("binary_sensor.daikin");
        manager.RemoveAsync("button.daikin");
        manager.RemoveAsync("switch.daikin");
        manager.RemoveAsync("water_heater.daikin");
        manager.RemoveAsync("water_heater.daikin2");
        manager.RemoveAsync("water_heater.10");
        manager.RemoveAsync("sensor.water_heater");
        manager.RemoveAsync("sensor.10");
        manager.RemoveAsync("switch.10");
        
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

                if (val.TryGetProperty("new_state", out var state))
                {
                    if (state.ValueKind == JsonValueKind.Object &&
                        state.TryGetProperty("attributes", out var attributes))
                    {
                        if (attributes.ValueKind == JsonValueKind.Object &&
                            attributes.TryGetProperty("friendly_name", out var friendlyName))
                        {
                            if (friendlyName.GetString() is not null)
                            {
                                tags.Add(
                                    new KeyValuePair<string, object?>("ha.friendly_name", friendlyName.GetString()));
                            }
                        }
                    }

                    if (state.ValueKind == JsonValueKind.Object && state.TryGetProperty("context", out var context))
                    {
                        if (context.ValueKind == JsonValueKind.Object &&
                            context.TryGetProperty("user_id", out var userIdElement))
                        {
                            if (userIdElement.GetString() is not null)
                            {
                                tags.Add(
                                    new KeyValuePair<string, object?>("ha.user_id", userIdElement.GetString()));
                            }
                        }
                    }
                }

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