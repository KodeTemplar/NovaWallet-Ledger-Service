namespace NovaWallet.Domain.Constants;

public static class WalletErrorCodes
{
    public const string WalletAlreadyExists = "WALLET_ALREADY_EXISTS";
    public const string WalletNotFound = "WALLET_NOT_FOUND";
    public const string InvalidCreditAmount = "INVALID_CREDIT_AMOUNT";
    public const string BalanceOverflow = "BALANCE_OVERFLOW";
    public const string InvalidTransferAmount = "INVALID_TRANSFER_AMOUNT";
    public const string SameWalletTransfer = "SAME_WALLET_TRANSFER";
    public const string InsufficientFunds = "INSUFFICIENT_FUNDS";
    public const string DailyOutboundLimitExceeded = "DAILY_OUTBOUND_LIMIT_EXCEEDED";
    public const string MoneyOverflow = "MONEY_OVERFLOW";
    public const string MissingIdempotencyKey = "MISSING_IDEMPOTENCY_KEY";
    public const string IdempotencyKeyConflict = "IDEMPOTENCY_KEY_CONFLICT";
    public const string IdempotencyRequestProcessing = "IDEMPOTENCY_REQUEST_PROCESSING";
    public const string InvalidStatementPage = "INVALID_STATEMENT_PAGE";
    public const string InvalidStatementPageSize = "INVALID_STATEMENT_PAGE_SIZE";
}
