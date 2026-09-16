namespace NovaWallet.Domain.Entities;

public class AuditLog
{
    public Guid Id { get; set; }

    public Guid WalletId { get; set; }

    public Guid? TransactionId { get; set; }

    public string MutationType { get; set; } = string.Empty;

    public long AmountKobo { get; set; }

    public long BalanceBeforeKobo { get; set; }

    public long BalanceAfterKobo { get; set; }

    public string ActorCustomerId { get; set; } = string.Empty;

    public string? MetadataJson { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
