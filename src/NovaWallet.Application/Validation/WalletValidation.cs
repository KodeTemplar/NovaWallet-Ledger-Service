using NovaWallet.Domain.Constants;

namespace NovaWallet.Application.Validation;

public static class WalletValidation
{
    public static bool IsValidCreditAmount(decimal amountKobo)
    {
        return amountKobo > 0;
    }

    public static string FormatAmount(long amountKobo)
    {
        return $"{CurrencyCodes.NairaSymbol}{amountKobo / 100m:N2}";
    }
}
