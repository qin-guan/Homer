using System.Text.Json.Serialization;
using Refit;

namespace Homer.NetDaemon.Apps.Kdk;

public record KdkAuthPostTokenRequest(
    [property: JsonPropertyName("refresh_token")]
    string RefreshToken,
    [property: JsonPropertyName("grant_type")]
    string GrantType = "refresh_token",
    [property: JsonPropertyName("client_id")]
    string ClientId = "cBobGHXGfRxMKXYXvMYevGPhMhFKpjsv"
);