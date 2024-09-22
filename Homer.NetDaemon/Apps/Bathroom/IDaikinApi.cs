using Refit;

namespace Homer.NetDaemon.Apps.Bathroom;

public interface IDaikinApi
{
    [Get("/api/devices")]
    public Task<DaikinApiGetDevicesResponse> GetDevices();
}