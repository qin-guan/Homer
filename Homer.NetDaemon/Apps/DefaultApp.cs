using System.Diagnostics;
using System.Reactive.Concurrency;
using System.Text.Json;
using Homer.ServiceDefaults.Metrics;
using NetDaemon.AppModel;
using NetDaemon.Extensions.MqttEntityManager;
using NetDaemon.HassModel;

namespace Homer.NetDaemon.Apps;

// [Focus]
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
        
        haContext.Events.Subscribe(e =>
        {
            // Only event type and entity domain are tagged: per-entity / friendly name / user tags multiplied
            // series count and forced a string allocation per property on every event of the HA bus.
            var domain = "none";
            if (e.EventType == "state_changed" && e.DataElement is { ValueKind: JsonValueKind.Object } data &&
                data.TryGetProperty("entity_id", out var entityId) &&
                entityId.GetString() is { } id)
            {
                var dot = id.IndexOf('.');
                domain = dot > 0 ? id[..dot] : "unknown";
            }

            EntityMetrics.HomeAssistantEvents.Add(1,
                new KeyValuePair<string, object?>("ha.event_type", e.EventType),
                new KeyValuePair<string, object?>("ha.domain", domain));
        });

        logger.LogInformation("Hello, home!");
    }
}