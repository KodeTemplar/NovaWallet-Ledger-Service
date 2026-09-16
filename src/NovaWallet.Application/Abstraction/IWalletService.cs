using NovaWallet.Application.Common.Responses;
using NovaWallet.Application.Models.Wallet;

namespace NovaWallet.Application.Abstraction;

public interface IWalletService
{
    Task<ApiResult> CreateWalletAsync(string customerId, CancellationToken cancellationToken = default);

    Task<ApiResult> GetWalletAsync(string customerId, CancellationToken cancellationToken = default);

    Task<ApiResult> CreditWalletAsync(CreditWalletRequest request, string walletId, string actorCustomerId, CancellationToken cancellationToken = default);
}
