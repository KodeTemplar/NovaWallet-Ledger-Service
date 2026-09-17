using NovaWallet.Domain.Constants;

namespace NovaWallet.Application.Validation;

public static class WalletValidation
{
    public static bool IsValidCreditAmount(long amountKobo)
    {
        return amountKobo > 0;
    }

    public static bool IsValidTransferAmount(long amountKobo)
    {
        return amountKobo > 0;
    }

    public static string FormatAmount(long amountKobo)
    {
        var wholeNaira = amountKobo / 100;
        var kobo = Math.Abs(amountKobo % 100);
        return $"{CurrencyCodes.NairaSymbol}{wholeNaira:N0}.{kobo:00}";
    }
}
