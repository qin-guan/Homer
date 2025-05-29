namespace Homer.NetDaemon.Services.SimplyGo.Transactions;

public record TransactionsResponse(
    string Code,
    Data Data,
    string Message
);

public record Data(
    TransactionsByDate[] TransactionsByDate,
    NextQueryDetail NextQueryDetail
);

public record TransactionsByDate(
    string Date,
    Transactions[] Transactions
);

public record Transactions(
    bool PhysicalCard,
    bool ClaimableTransaction,
    int No,
    string Type,
    string MccDescription,
    string EntryTransactionDate,
    string ExitTransactionDate,
    int EntryLocationId,
    string EntryLocationName,
    int ExitLocationId,
    string ExitLocationName,
    string Fare,
    string Status,
    double Discount,
    double Surcharge,
    bool IsPhysicalCard,
    string BankAuthorizedDate,
    string BankTaxRefNo,
    string TokenId,
    string PassUsageJourney,
    bool IsClaimableTransaction,
    Trips[] Trips,
    object ThFundIssuerAuthorizedDate,
    string Can,
    double RawAmount,
    string DisplayTxnAmount,
    string TxnDisplayTime,
    string TxnCategory,
    string CategoryDescription
);

public record Trips(
    bool ClaimableTransaction,
    string Uid,
    int No,
    string EntryTransactionDate,
    string ExitTransactionDate,
    string TransactionType,
    string MccDescription,
    string BusServiceNo,
    string BusDirection,
    int EntryLocationId,
    string EntryLocationName,
    int ExitLocationId,
    string ExitLocationName,
    string Fare,
    string Status,
    double Discount,
    double Surcharge,
    string SurchargeReason,
    string PassUsage,
    int TripAutoComplete,
    bool IsClaimableTransaction,
    string TabaBusServiceNo,
    string OriBoardingBusStopCode,
    string OriAlightingBusStopCode
);

public record NextQueryDetail(
    string StartDate,
    string EndDate,
    string CardCreatedDate,
    string LastTxnDate,
    object TokenizedDate,
    bool FetchAll
);

