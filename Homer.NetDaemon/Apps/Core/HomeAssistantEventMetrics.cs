using System.Text.Json;
using Homer.ServiceDefaults.Metrics;
using NetDaemon.AppModel;
using NetDaemon.HassModel;

namespace Homer.NetDaemon.Apps.Core;

/// <summary>Counts Home Assistant bus traffic by event type and, for state changes, entity domain.</summary>
[NetDaemonApp]
public class HomeAssistantEventMetrics
{
    public HomeAssistantEventMetrics(IHaContext haContext)
    {
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
    }
}
