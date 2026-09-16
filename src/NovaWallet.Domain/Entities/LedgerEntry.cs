using NovaWallet.Domain.Enums;

namespace NovaWallet.Domain.Entities;

public class LedgerEntry
{
    public Guid Id { get; set; }

    public Guid TransactionId { get; set; }

    public Guid WalletId { get; set; }

    public LedgerDirection Direction { get; set; }

    public long AmountKobo { get; set; }

    public long BalanceAfterKobo { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
