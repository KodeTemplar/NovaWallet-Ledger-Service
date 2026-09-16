using NovaWallet.Domain.Constants;
using NovaWallet.Domain.Entities;
using NovaWallet.Application.Validation;

namespace NovaWallet.UnitTests;

public class DomainFoundationTests
{
    [Fact]
    public void Wallet_UsesLongKoboBalanceAndNgnDefaultCurrency()
    {
        var wallet = new Wallet();

        Assert.Equal(CurrencyCodes.Ngn, wallet.Currency);
        Assert.IsType<long>(wallet.BalanceKobo);
    }

    [Fact]
    public void DailyOutboundTransferLimit_IsStoredAsKobo()
    {
        Assert.Equal(50_000_000L, FinancialLimits.DailyOutboundTransferLimitKobo);
    }

    [Fact]
    public void CreditAmountOfZero_IsRejected()
    {
        Assert.False(WalletValidation.IsValidCreditAmount(0));
    }

    [Fact]
    public void NegativeCreditAmount_IsRejected()
    {
        Assert.False(WalletValidation.IsValidCreditAmount(-1));
    }
}
