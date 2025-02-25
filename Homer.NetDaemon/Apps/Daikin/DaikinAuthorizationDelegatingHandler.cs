using System.Net.Http.Headers;
using Homer.NetDaemon.Options;
using Microsoft.Extensions.Options;

namespace Homer.NetDaemon.Apps.Daikin;

public class DaikinAuthorizationDelegatingHandler(
        ILogger<DaikinAuthorizationDelegatingHandler> logger,
    IOptions<DaikinOptions> daikinOptions,
    IHttpClientFactory httpClientFactory
) : DelegatingHandler
{
    private string? _token;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(daikinOptions.Value.Username))
        {
            logger.LogWarning("Daikin username is not set. Daikin apps will not work.");
        }

        if (_token is null)
        {
            using var client = httpClientFactory.CreateClient();
            var res = await client.PostAsJsonAsync(
                "https://appdaikin.ez1.cloud:8443/api/login",
                new
                {
                    daikinOptions.Value.Username,
                    daikinOptions.Value.Password,
                    Deviceid = "iPhone15,3",
                    Devicename = "iPhone",
                    Appsid = "eziot732280"
                },
                cancellationToken: cancellationToken
            );
            res.EnsureSuccessStatusCode();
            var content =
                await res.Content.ReadFromJsonAsync<DaikinApiPostLoginResponse>(cancellationToken: cancellationToken);
            ArgumentNullException.ThrowIfNull(content);
            _token = content.Response.AccessToken;
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);

        return await base.SendAsync(request, cancellationToken);
    }
}
