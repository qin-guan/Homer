using Refit;

namespace Homer.NetDaemon.Apps.Kdk;

public interface IKdkApi
{
    [Post("/v1/mycfan/deviceControls")]
    public Task<HttpResponseMessage> SetDeviceControlsAsync([Body] KdkApiPostDeviceControlsRequest data);
}