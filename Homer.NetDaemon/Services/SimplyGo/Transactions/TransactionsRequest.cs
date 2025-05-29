namespace Homer.NetDaemon.Services.SimplyGo.Transactions;

public class TransactionsRequest
{
    public string LastTxnDate { get; set; }
    public bool FetchAll { get; set; }
    public string CardCreatedDate { get; set; }
    public string TokenizedDate { get; set; }
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
}
