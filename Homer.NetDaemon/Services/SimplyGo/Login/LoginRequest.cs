using System.Text.Json.Serialization;

namespace Homer.NetDaemon.Services.SimplyGo.Login;

public class LoginRequest
{
    [JsonPropertyName("mobile_or_email")] public string MobileOrEmail { get; set; }
    [JsonPropertyName("password")] public string Password { get; set; }
    [JsonPropertyName("pin_code")] public string PinCode { get; set; } = "";
    [JsonPropertyName("device_name")] public string DeviceName { get; set; } = "iPhone";
    [JsonPropertyName("device_id")] public string DeviceId { get; set; } = "D7E99036-1F8E-43CF-89E2-3E5DD84FB9A7";
}