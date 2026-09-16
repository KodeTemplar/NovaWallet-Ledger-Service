using NovaWallet.Domain.Enums;

namespace NovaWallet.Domain.Entities;

public class Transaction
{
    public Guid Id { get; set; }

    public TransactionType Type { get; set; }

    public Guid? SourceWalletId { get; set; }

    public Guid? DestinationWalletId { get; set; }

    public long AmountKobo { get; set; }

    public string Reference { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
}
