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
    private readonly IFinancialLimitService _financialLimitService;

    public WalletService(NovaWalletDbContext dbContext, IAuditLogService auditLogService, IFinancialLimitService financialLimitService)
    {
        _dbContext = dbContext;
        _auditLogService = auditLogService;
        _financialLimitService = financialLimitService;
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
        if (!WalletValidation.TryConvertNairaToKobo(request.Amount, out var amountKobo))
        {
            return ApiProblem.Validation("Credit amount must be greater than zero.", WalletErrorCodes.InvalidCreditAmount);
        }

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

    public async Task<ApiResult> TransferAsync(string customerId, TransferRequest request, CancellationToken cancellationToken = default)
    {
        if (!WalletValidation.TryConvertNairaToKobo(request.Amount, out var amountKobo))
        {
            return ApiProblem.Validation("Transfer amount must be greater than zero and have at most two decimal places.", WalletErrorCodes.InvalidTransferAmount);
        }

        var sourceWalletId = await _dbContext.Wallets
            .AsNoTracking()
            .Where(wallet => wallet.CustomerId == customerId)
            .Select(wallet => (Guid?)wallet.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (sourceWalletId == null)
        {
            return ApiProblem.NotFound("Source wallet was not found for this customer.", WalletErrorCodes.WalletNotFound);
        }

        if (sourceWalletId.Value == request.DestinationWalletId)
        {
            return ApiProblem.Conflict("Source and destination wallets must be different.", WalletErrorCodes.SameWalletTransfer);
        }

        await using var transactionScope = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Both wallets are locked in deterministic order so opposite-direction transfers do not deadlock by taking locks in reverse order.
        var sourceId = sourceWalletId.Value;
        var destinationId = request.DestinationWalletId;

        var sourceComesFirst = sourceId.CompareTo(destinationId) <= 0;

        var firstWalletId = sourceComesFirst ? sourceId : destinationId;
        var secondWalletId = sourceComesFirst ? destinationId : sourceId;

        var firstWallet = await LockWalletAsync(firstWalletId, cancellationToken);
        var secondWallet = await LockWalletAsync(secondWalletId, cancellationToken);

        var sourceWallet = firstWallet?.Id == sourceId ? firstWallet : secondWallet;
        var destinationWallet = firstWallet?.Id == destinationId ? firstWallet : secondWallet;

        if (sourceWallet == null)
        {
            return ApiProblem.NotFound("Source wallet was not found for this customer.", WalletErrorCodes.WalletNotFound);
        }

        if (destinationWallet == null)
        {
            return ApiProblem.NotFound("Destination wallet was not found.", WalletErrorCodes.WalletNotFound);
        }

        if (sourceWallet.BalanceKobo < amountKobo)
        {
            return ApiProblem.Unprocessable("Sorry, you do not have sufficient funds in your wallet.", WalletErrorCodes.InsufficientFunds);
        }

        var dailyLimitKobo = await _financialLimitService.GetDailyOutboundTransferLimitKoboAsync(cancellationToken);
        var watDate = GetCurrentWatDate();
        var dailyTransferLimit = await GetOrCreateDailyTransferLimitAsync(sourceWallet.Id, watDate, cancellationToken);

        long sourceBalanceAfter;
        long destinationBalanceAfter;
        long outboundTotalAfter;

        try
        {
            sourceBalanceAfter = checked(sourceWallet.BalanceKobo - amountKobo);
            destinationBalanceAfter = checked(destinationWallet.BalanceKobo + amountKobo);
            outboundTotalAfter = checked(dailyTransferLimit.OutboundTotalKobo + amountKobo);
        }
        catch (OverflowException)
        {
            return ApiProblem.Unprocessable("The transfer amount would exceed supported money limits.", WalletErrorCodes.MoneyOverflow);
        }

        if (outboundTotalAfter > dailyLimitKobo)
        {
            return ApiProblem.Unprocessable("Daily outbound transfer limit would be exceeded.", WalletErrorCodes.DailyOutboundLimitExceeded);
        }

        var now = DateTimeOffset.UtcNow;
        var transactionId = Guid.NewGuid();
        var reference = $"TRF-{Guid.NewGuid():N}";
        var sourceBalanceBefore = sourceWallet.BalanceKobo;
        var destinationBalanceBefore = destinationWallet.BalanceKobo;

        sourceWallet.BalanceKobo = sourceBalanceAfter;
        destinationWallet.BalanceKobo = destinationBalanceAfter;
        dailyTransferLimit.OutboundTotalKobo = outboundTotalAfter;

        var financialTransaction = new Transaction
        {
            Id = transactionId,
            Type = TransactionType.Transfer,
            SourceWalletId = sourceWallet.Id,
            DestinationWalletId = destinationWallet.Id,
            AmountKobo = amountKobo,
            Reference = reference,
            CreatedAtUtc = now
        };

        var sourceLedgerEntry = new LedgerEntry
        {
            Id = Guid.NewGuid(),
            TransactionId = transactionId,
            WalletId = sourceWallet.Id,
            Direction = LedgerDirection.Debit,
            AmountKobo = amountKobo,
            BalanceAfterKobo = sourceBalanceAfter,
            CreatedAtUtc = now
        };

        var destinationLedgerEntry = new LedgerEntry
        {
            Id = Guid.NewGuid(),
            TransactionId = transactionId,
            WalletId = destinationWallet.Id,
            Direction = LedgerDirection.Credit,
            AmountKobo = amountKobo,
            BalanceAfterKobo = destinationBalanceAfter,
            CreatedAtUtc = now
        };

        _dbContext.Transactions.Add(financialTransaction);
        _dbContext.LedgerEntries.AddRange(sourceLedgerEntry, destinationLedgerEntry);

        await _auditLogService.AddAsync(new AuditRequest
        {
            WalletId = sourceWallet.Id,
            TransactionId = transactionId,
            MutationType = AuditMutationTypes.TransferDebit,
            AmountKobo = amountKobo,
            BalanceBeforeKobo = sourceBalanceBefore,
            BalanceAfterKobo = sourceBalanceAfter,
            ActorCustomerId = customerId
        }, cancellationToken);

        await _auditLogService.AddAsync(new AuditRequest
        {
            WalletId = destinationWallet.Id,
            TransactionId = transactionId,
            MutationType = AuditMutationTypes.TransferCredit,
            AmountKobo = amountKobo,
            BalanceBeforeKobo = destinationBalanceBefore,
            BalanceAfterKobo = destinationBalanceAfter,
            ActorCustomerId = customerId
        }, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transactionScope.CommitAsync(cancellationToken);

        var response = new TransferResponse
        {
            TransactionId = transactionId,
            Reference = reference,
            SourceCustomerId = sourceWallet.Id,
            AmountKobo = amountKobo,
            SourceBalanceAfterKobo = sourceBalanceAfter,
            FormatedBalance = WalletValidation.FormatAmount(sourceWallet.BalanceKobo),
            CreatedAtUtc = now
        };

        return ApiSuccess<TransferResponse>.Success(response, "Transfer completed successfully.");
    }

    private async Task<Wallet?> LockWalletAsync(Guid walletId, CancellationToken cancellationToken)
    {
        return await _dbContext.Wallets
            .FromSqlInterpolated($"SELECT * FROM [Wallets] WITH (UPDLOCK, ROWLOCK) WHERE [Id] = {walletId}")
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<DailyTransferLimit> GetOrCreateDailyTransferLimitAsync(Guid walletId, DateOnly watDate, CancellationToken cancellationToken)
    {
        var dailyTransferLimit = await _dbContext.DailyTransferLimits
            .FromSqlInterpolated($"SELECT * FROM [DailyTransferLimits] WITH (UPDLOCK, ROWLOCK) WHERE [WalletId] = {walletId} AND [WatDate] = {watDate}")
            .SingleOrDefaultAsync(cancellationToken);

        if (dailyTransferLimit != null)
        {
            return dailyTransferLimit;
        }

        dailyTransferLimit = new DailyTransferLimit
        {
            WalletId = walletId,
            WatDate = watDate,
            OutboundTotalKobo = 0
        };

        await _dbContext.DailyTransferLimits.AddAsync(dailyTransferLimit, cancellationToken);

        return dailyTransferLimit;
    }

    private static DateOnly GetCurrentWatDate()
    {
        var watNow = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(1));
        return DateOnly.FromDateTime(watNow.DateTime);
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
