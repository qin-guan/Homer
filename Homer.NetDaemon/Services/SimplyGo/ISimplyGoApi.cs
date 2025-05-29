using Homer.NetDaemon.Services.SimplyGo.Login;
using Homer.NetDaemon.Services.SimplyGo.Transactions;
using Refit;

namespace Homer.NetDaemon.Services.SimplyGo;

public interface ISimplyGoApi
{
    [Post("/v2/users/login")]
    public Task<LoginResponse> Login([Body] LoginRequest request, CancellationToken ct = default);

    [Post("/v5/abt/8000240001266761/transactions")]
    public Task<TransactionsResponse> GetTransactions(
        [Body] TransactionsRequest request,
        [Header("X-CLIENT-ID")] string clientId,
        [Header("X-AUTH-TOKEN")] string authToken,
        CancellationToken ct = default
    );
}