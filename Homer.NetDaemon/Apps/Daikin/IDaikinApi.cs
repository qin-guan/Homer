using Refit;

namespace Homer.NetDaemon.Apps.Daikin;

public interface IDaikinApi
{
    [Get("/api/devices")]
    public Task<DaikinApiGetDevicesResponse> GetDevicesAsync();

    [Post("/api/device/{deviceId}/status")]
    public Task UpdateDeviceStatusAsync(int deviceId, [Body] DaikinApiPostDeviceStatus body);
}