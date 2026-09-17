namespace NovaWallet.Application.Models.Wallet;

public class StatementItemResponse
{
    public Guid TransactionId { get; set; }

    public string Reference { get; set; } = string.Empty;

    public string TransactionType { get; set; } = string.Empty;

    public string Direction { get; set; } = string.Empty;

    public long AmountKobo { get; set; }

    public long BalanceAfterKobo { get; set; }

    public Guid? CounterpartyWalletId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
