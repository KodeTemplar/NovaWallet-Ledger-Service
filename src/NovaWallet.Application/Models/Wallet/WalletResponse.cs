namespace NovaWallet.Application.Models.Wallet;

public class WalletResponse
{
    public Guid WalletId { get; set; }

    public string CustomerId { get; set; } = string.Empty;

    public string Currency { get; set; } = string.Empty;

    public long BalanceKobo { get; set; }

    public string FormattedBalance { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
}
