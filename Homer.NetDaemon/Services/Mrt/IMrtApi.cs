using Homer.NetDaemon.Services.Mrt.Distance;
using Refit;

namespace Homer.NetDaemon.Services.Mrt;

public interface IMrtApi
{
    [Get("/Distance")]
    public Task<DistanceResponse> GetDistance([Query("from")] string from, [Query("to")] string to, CancellationToken ct = default);
}