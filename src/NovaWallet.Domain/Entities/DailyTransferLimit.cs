namespace NovaWallet.Domain.Entities;

public class DailyTransferLimit
{
    public Guid WalletId { get; set; }

    public DateOnly WatDate { get; set; }

    public long OutboundTotalKobo { get; set; }
}
