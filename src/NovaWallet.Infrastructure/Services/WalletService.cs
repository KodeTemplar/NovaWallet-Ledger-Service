using Microsoft.EntityFrameworkCore;
using NovaWallet.Application.Abstraction;
using NovaWallet.Application.Common;
using NovaWallet.Application.Common.Responses;
using NovaWallet.Application.Models.Audit;
using NovaWallet.Application.Models.Wallet;
using NovaWallet.Application.Validation;
using NovaWallet.Domain.Constants;
using NovaWallet.Domain.Entities;
using NovaWallet.Domain.Enums;
using NovaWallet.Infrastructure.Persistence;

namespace NovaWallet.Infrastructure.Services;

public class WalletService : IWalletService
{
    private readonly NovaWalletDbContext _dbContext;
    private readonly IAuditLogService _auditLogService;

    public WalletService(NovaWalletDbContext dbContext, IAuditLogService auditLogService)
    {
        _dbContext = dbContext;
        _auditLogService = auditLogService;
    }

    public async Task<ApiResult> CreateWalletAsync(string customerId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(customerId))
        {
            return ApiProblem.Validation("CustomerId is required.");
        }

        var walletExists = await _dbContext.Wallets.AnyAsync(wallet => wallet.CustomerId == customerId, cancellationToken);

        if (walletExists)
        {
            return ApiProblem.Conflict("Wallet already exists for this customer.", WalletErrorCodes.WalletAlreadyExists);
        }

        var wallet = new Wallet
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            Currency = CurrencyCodes.Ngn,
            BalanceKobo = 0,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        await _dbContext.Wallets.AddAsync(wallet, cancellationToken);
        var auditRequest = new AuditRequest
        {
            WalletId = wallet.Id,
            TransactionId = null,
            MutationType = AuditMutationTypes.WalletCreated,
            AmountKobo = 0,
            BalanceBeforeKobo = 0,
            BalanceAfterKobo = 0,
            ActorCustomerId = customerId
        };

        await _auditLogService.AddAsync(auditRequest, cancellationToken);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return ApiProblem.Conflict("Wallet already exists for this customer.", WalletErrorCodes.WalletAlreadyExists);
        }

        return ApiSuccess<WalletResponse>.Success(ToWalletResponse(wallet), "Wallet created successfully.");
    }

    public async Task<ApiResult> GetWalletAsync(string customerId, CancellationToken cancellationToken = default)
    {
        var wallet = await _dbContext.Wallets
            .AsNoTracking()
            .SingleOrDefaultAsync(existingWallet => existingWallet.CustomerId == customerId, cancellationToken);

        if (wallet == null)
        {
            return ApiProblem.NotFound("Wallet was not found for this customer.", WalletErrorCodes.WalletNotFound);
        }

        return ApiSuccess<WalletResponse>.Success(ToWalletResponse(wallet), "Wallet retrieved successfully.");
    }

    public async Task<ApiResult> CreditWalletAsync(CreditWalletRequest request, string walletId, string actorCustomerId, CancellationToken cancellationToken = default)
    {
        if (!WalletValidation.IsValidCreditAmount(request.Amount))
        {
            return ApiProblem.Validation("Credit amount must be greater than zero.", WalletErrorCodes.InvalidCreditAmount);
        }

        var amountKobo = checked((long)(request.Amount * 100));

        await using var transactionScope = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        // UPDLOCK serializes concurrent credits to this wallet so one request cannot overwrite another request's balance update.
        var wallet = await _dbContext.Wallets
            .FromSqlInterpolated($"SELECT * FROM [Wallets] WITH (UPDLOCK, ROWLOCK) WHERE [Id] = {walletId}")
            .SingleOrDefaultAsync(cancellationToken);

        if (wallet == null)
        {
            return ApiProblem.NotFound("Wallet was not found.", WalletErrorCodes.WalletNotFound);
        }

        var now = DateTimeOffset.UtcNow;
        var balanceBefore = wallet.BalanceKobo;
        long balanceAfter = 0;
        try
        {
            balanceAfter = checked(balanceBefore + amountKobo);
        }
        catch (OverflowException)
        {
            return ApiProblem.Unprocessable("The credit amount would exceed the maximum supported wallet balance.", WalletErrorCodes.BalanceOverflow);
        }
        var transactionReference = $"NIP-{Guid.NewGuid():N}".Trim();

        wallet.BalanceKobo = balanceAfter;

        var financialTransaction = new Transaction
        {
            Id = Guid.NewGuid(),
            Type = TransactionType.Credit,
            SourceWalletId = null,
            DestinationWalletId = wallet.Id,
            AmountKobo = amountKobo,
            Reference = transactionReference,
            CreatedAtUtc = now
        };

        var ledgerEntry = new LedgerEntry
        {
            Id = Guid.NewGuid(),
            TransactionId = financialTransaction.Id,
            WalletId = wallet.Id,
            Direction = LedgerDirection.Credit,
            AmountKobo = amountKobo,
            BalanceAfterKobo = balanceAfter,
            CreatedAtUtc = now
        };

        var auditRequest = new AuditRequest
        {
            WalletId = wallet.Id,
            TransactionId = financialTransaction.Id,
            MutationType = AuditMutationTypes.Credit,
            AmountKobo = amountKobo,
            BalanceBeforeKobo = balanceBefore,
            BalanceAfterKobo = balanceAfter,
            ActorCustomerId = actorCustomerId
        };

        await _auditLogService.AddAsync(auditRequest, cancellationToken);

        _dbContext.Transactions.Add(financialTransaction);
        _dbContext.LedgerEntries.Add(ledgerEntry);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transactionScope.CommitAsync(cancellationToken);

        return ApiSuccess<WalletResponse>.Success(ToWalletResponse(wallet), "Wallet credited successfully.");
    }

    private static WalletResponse ToWalletResponse(Wallet wallet)
    {
        return new WalletResponse
        {
            WalletId = wallet.Id,
            CustomerId = wallet.CustomerId,
            Currency = wallet.Currency,
            Balance = wallet.BalanceKobo / 100m,
            BalanceKobo = wallet.BalanceKobo,
            FormattedBalance = WalletValidation.FormatAmount(wallet.BalanceKobo),
            CreatedAtUtc = wallet.CreatedAtUtc
        };
    }
}
