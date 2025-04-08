using WebApiClientCore.Attributes;

namespace Homer.NetDaemon.Apps.Kdk;

[Header("User-Agent", "Ceiling Fan/1.1.0 (iPhone; iOS 18.0.1; Scale/3.00)")]
public interface IKdkApi
{
    [HttpPost("/v1/mycfan/deviceControls")]
    public Task<HttpResponseMessage> SetDeviceControlsAsync([JsonContent] KdkApiPostDeviceControlsRequest data);
}