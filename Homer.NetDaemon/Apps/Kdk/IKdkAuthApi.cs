using WebApiClientCore.Attributes;

namespace Homer.NetDaemon.Apps.Kdk;

public interface IKdkAuthApi
{
    [HttpPost("/oauth/token")]
    public Task<KdkAuthPostTokenResponse>
        TokenAsync([FormContent] KdkAuthPostTokenRequest data);
    
    [HttpPost("/oauth/token")]
    public Task<HttpResponseMessage>
        DebugTokenAsync([FormContent] KdkAuthPostTokenRequest data);
}