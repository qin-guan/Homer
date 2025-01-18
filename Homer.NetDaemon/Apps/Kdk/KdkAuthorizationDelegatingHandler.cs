using Homer.NetDaemon.Entities;
using Homer.NetDaemon.Options;
using Microsoft.Extensions.Options;

namespace Homer.NetDaemon.Apps.Kdk;

public class KdkAuthorizationDelegatingHandler(
    IKdkAuthApi authApi,
    NotifyServices notifyServices,
    InputTextEntities inputTextEntities,
    IOptions<KdkOptions> kdkOptions
) : DelegatingHandler
{
    private DateTime _lastTokenRefresh = DateTime.MinValue;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (kdkOptions.Value.BearerToken is null || (DateTime.Now - _lastTokenRefresh).TotalHours >= 12)
        {
            var res = await authApi.TokenAsync(
                new KdkAuthPostTokenRequest(inputTextEntities.KdkFanRefreshToken.State ?? kdkOptions.Value.RefreshToken)
            );

            kdkOptions.Value.BearerToken = res.AccessToken;
            _lastTokenRefresh = DateTime.Now;

            notifyServices.PersistentNotification(res.AccessToken, "New KDK Access Token");

            if (res.RefreshToken is not null)
            {
                kdkOptions.Value.RefreshToken = res.RefreshToken;
                inputTextEntities.KdkFanRefreshToken.SetValue(res.RefreshToken);
                notifyServices.PersistentNotification(res.RefreshToken, "New KDK Refresh Token");
            }
        }

        request.Headers.Add("Authorization", kdkOptions.Value.BearerToken);

        return await base.SendAsync(request, cancellationToken);
    }
}