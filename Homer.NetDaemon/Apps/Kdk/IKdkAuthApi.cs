using Refit;

namespace Homer.NetDaemon.Apps.Kdk;

public interface IKdkAuthApi
{
    [Post("/oauth/token")]
    public Task<KdkAuthPostTokenResponse>
        TokenAsync([Body(BodySerializationMethod.UrlEncoded)] KdkAuthPostTokenRequest data);
    
    [Post("/oauth/token")]
    public Task<HttpResponseMessage>
        DebugTokenAsync([Body(BodySerializationMethod.UrlEncoded)] KdkAuthPostTokenRequest data);
}