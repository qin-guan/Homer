using System.Text.Json.Serialization;

namespace Homer.NetDaemon.Apps.Daikin;

public record DaikinApiPostDeviceStatus(DaikinApiPostDeviceStatus.InnerParameters Parameters, string Action = "control")
{
    public record InnerParameters(
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        int? TemperatureSetting = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        string? HeaterStatus = null
    );
}