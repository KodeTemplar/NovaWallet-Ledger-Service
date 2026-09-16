namespace NovaWallet.Application.Models.Wallet;

public class TransferResponse
{
    public Guid TransactionId { get; set; }

    public string Reference { get; set; } = string.Empty;

    public string FormatedBalance { get; set; }

    public Guid SourceCustomerId { get; set; }

    public long AmountKobo { get; set; }

    public long SourceBalanceAfterKobo { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
