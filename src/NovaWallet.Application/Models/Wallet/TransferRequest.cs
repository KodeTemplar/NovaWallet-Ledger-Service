namespace NovaWallet.Application.Models.Wallet;

public class TransferRequest
{
    public Guid DestinationWalletId { get; set; }

    public decimal Amount { get; set; }
}
