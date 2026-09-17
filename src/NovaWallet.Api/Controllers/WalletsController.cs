using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NovaWallet.Api.Extensions;
using NovaWallet.Application.Abstraction;
using NovaWallet.Application.Common.Responses;
using NovaWallet.Application.Models.Wallet;
using NovaWallet.Domain.Constants;

namespace NovaWallet.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/wallets")]
public class WalletsController : ControllerBase
{
    private readonly IWalletService _walletService;
    private readonly ICurrentCustomer _currentCustomer;

    public WalletsController(IWalletService walletService, ICurrentCustomer currentCustomer)
    {
        _walletService = walletService;
        _currentCustomer = currentCustomer;
    }

    [HttpPost]
    public async Task<IActionResult> CreateWallet(CancellationToken cancellationToken)
    {
        var customerId = _currentCustomer.CustomerId;

        if (string.IsNullOrWhiteSpace(customerId))
        {
            return this.ApiProblem(ApiProblem.Validation("Authenticated customer_id claim is required."));
        }

        var result = await _walletService.CreateWalletAsync(customerId, cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetWallet(CancellationToken cancellationToken)
    {
        var customerId = _currentCustomer.CustomerId;

        if (string.IsNullOrWhiteSpace(customerId))
        {
            return this.ApiProblem(ApiProblem.Validation("Authenticated customer_id claim is required."));
        }

        var result = await _walletService.GetWalletAsync(customerId, cancellationToken);

        return this.ToActionResult(result);
    }

    [Authorize(Policy = AuthorizationPolicies.PrivilegedWalletCredit)]
    [HttpPost("{walletId:guid}/credits")]
    public async Task<IActionResult> CreditWallet(Guid walletId, CreditWalletRequest request, CancellationToken cancellationToken)
    {
        var actorCustomerId = _currentCustomer.CustomerId ?? "unknown";
        var result = await _walletService.CreditWalletAsync(request, walletId.ToString(), actorCustomerId, cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPost("transfers")]
    [EnableRateLimiting("TransferRateLimit")]
    public async Task<IActionResult> Transfer([FromBody] TransferRequest request, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken cancellationToken)
    {
        var customerId = _currentCustomer.CustomerId;

        if (string.IsNullOrWhiteSpace(customerId))
        {
            return this.ApiProblem(ApiProblem.Validation("Authenticated customer_id claim is required."));
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return this.ApiProblem(ApiProblem.Validation("Idempotency-Key header is required.", WalletErrorCodes.MissingIdempotencyKey));
        }

        var result = await _walletService.TransferAsync(customerId, idempotencyKey, request, cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("me/statements")]
    public async Task<IActionResult> GetStatement([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var customerId = _currentCustomer.CustomerId;

        if (string.IsNullOrWhiteSpace(customerId))
        {
            return this.ApiProblem(ApiProblem.Validation("Authenticated customer_id claim is required."));
        }

        var result = await _walletService.GetStatementAsync(customerId, page, pageSize, cancellationToken);

        return this.ToActionResult(result);
    }
}
