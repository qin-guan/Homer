using System.Reactive.Concurrency;
using Homer.NetDaemon.Entities;
using Homer.NetDaemon.Options;
using Homer.NetDaemon.Services.Mrt;
using Homer.NetDaemon.Services.SimplyGo;
using Homer.NetDaemon.Services.SimplyGo.Login;
using Homer.NetDaemon.Services.SimplyGo.Transactions;
using Microsoft.Extensions.Options;
using NetDaemon.AppModel;

namespace Homer.NetDaemon.Apps;

[Focus]
[NetDaemonApp]
public class SimplyGoConcessionMonitor(
    ISimplyGoApi api,
    IMrtApi mrt,
    NotifyServices notify,
    IScheduler scheduler,
    IOptions<SimplyGoOptions> options
) : IAsyncInitializable
{
    private string? _accessToken;
    private decimal? _currentTotal;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var res = await api.Login(new LoginRequest
        {
            MobileOrEmail = options.Value.Phone,
            Password = options.Value.Password
        }, cancellationToken);

        _accessToken = res.Data.AccessToken;

        scheduler.ScheduleAsync(TimeSpan.FromMinutes(1), async (_, ct) =>
        {
            string? startDate = null;
            string? endDate = null;

            var all = new List<Transactions>();

            for (var i = 0; i < 100; i++)
            {
                if (cancellationToken.IsCancellationRequested) break;
                if (startDate is not null &&
                    !startDate.StartsWith($"{DateTime.Now.Year}{DateTime.Now.Month:D2}")) break;

                var trips = await api.GetTransactions(new TransactionsRequest
                {
                    FetchAll = false,
                    TokenizedDate = "",
                    LastTxnDate = "20250526",
                    CardCreatedDate = "20240704",
                    StartDate = startDate,
                    EndDate = endDate
                }, res.Data.ClientId, _accessToken, cancellationToken);

                startDate = trips.Data.NextQueryDetail?.StartDate;
                endDate = trips.Data.NextQueryDetail?.EndDate;

                all.AddRange(trips.Data.TransactionsByDate.SelectMany(t => t.Transactions));

                if (startDate is null) break;
            }

            var concessionTransactions = all
                .Where(t => t is { Fare : "Pass Usage", EntryLocationName.Length: > 0, ExitLocationName.Length: > 0 })
                .Select(t => t with
                {
                    EntryLocationName = t.EntryLocationName
                        .Replace("CCL", "")
                        .Replace("NSEW", "")
                        .Replace("NEL", "")
                        .Trim(),
                    ExitLocationName = t.ExitLocationName
                        .Replace("CCL", "")
                        .Replace("NSEW", "")
                        .Replace("NEL", "")
                        .Trim()
                });

            decimal total = 0;
            var l = new Lock();

            await Parallel.ForEachAsync(concessionTransactions, ct, async (t, ct2) =>
            {
                var cost = await mrt.GetDistance(t.EntryLocationName, t.ExitLocationName, ct2);
                lock (l)
                {
                    total += cost.Student;
                }
            });

            var diff = _currentTotal - total;

            _currentTotal = total;
            notify.MobileAppQinsIphone($"Latest trip: {diff}. Total saved: {total}");
        });
    }
}