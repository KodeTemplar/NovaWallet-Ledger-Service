namespace NovaWallet.Application.Models.Wallet;

public class TransferRequest
{
    public Guid DestinationWalletId { get; set; }

    public long AmountKobo { get; set; }
}
