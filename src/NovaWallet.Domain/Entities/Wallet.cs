using NovaWallet.Domain.Constants;

namespace NovaWallet.Domain.Entities;

public class Wallet
{
    public Guid Id { get; set; }

    public string CustomerId { get; set; } = string.Empty;

    public string Currency { get; set; } = CurrencyCodes.Ngn;

    public long BalanceKobo { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
