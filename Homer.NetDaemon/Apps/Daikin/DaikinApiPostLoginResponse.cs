using System.Text.Json.Serialization;

namespace Homer.NetDaemon.Apps.Daikin;

public record DaikinApiPostLoginResponse(DaikinApiPostLoginResponse.InnerResponse Response)
{
    public record InnerResponse(
        [property: JsonPropertyName("access_token")]
        string AccessToken
    );
};