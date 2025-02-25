using System.Text.Json.Serialization;

namespace Homer.NetDaemon.Apps.Kdk;

public record KdkApiPostDeviceControlsRequest(
    [property: JsonPropertyName("appliance_id")]
    string ApplianceId,
    string Packet,
    string Method = "SET"
);