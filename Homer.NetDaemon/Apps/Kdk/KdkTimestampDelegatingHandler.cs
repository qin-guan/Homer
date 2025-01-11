namespace Homer.NetDaemon.Apps.Kdk;

public class KdkTimestampDelegatingHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        request.Headers.Add("X-Timestamp", $"{now.Year}{now.Month}{now.Day}{now.Hour}{now.Minute}{now.Second}+0000");
        return await base.SendAsync(request, cancellationToken);
    }
}