using NovaWallet.Domain.Constants;

namespace NovaWallet.Application.Validation;

public static class WalletValidation
{
    public static bool IsValidCreditAmount(decimal amount)
    {
        return amount > 0;
    }

    public static bool IsValidTransferAmount(decimal amount)
    {
        return amount > 0 && HasAtMostTwoDecimalPlaces(amount);
    }

    public static bool TryConvertNairaToKobo(decimal amount, out long amountKobo)
    {
        amountKobo = 0;

        if (amount <= 0 || !HasAtMostTwoDecimalPlaces(amount) || amount > long.MaxValue / 100m)
        {
            return false;
        }

        amountKobo = checked((long)(amount * 100m));
        return true;
    }

    public static string FormatAmount(long amountKobo)
    {
        return $"{CurrencyCodes.NairaSymbol}{amountKobo / 100m:N2}";
    }

    private static bool HasAtMostTwoDecimalPlaces(decimal amount)
    {
        return decimal.Round(amount, 2) == amount;
    }
}
