using Microsoft.AspNetCore.Mvc;
using Refit;

namespace Homer.NetDaemon.Apps.Kdk;

[Headers("User-Agent", "Ceiling Fan/1.1.0 (iPhone; iOS 18.0.1; Scale/3.00)")]
public interface IKdkApi
{
    [Post("/v1/mycfan/deviceControls")]
    public Task<HttpResponseMessage> SetDeviceControlsAsync([Body] KdkApiPostDeviceControlsRequest data);
}