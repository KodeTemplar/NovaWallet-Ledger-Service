using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NovaWallet.Domain.Constants;
using NovaWallet.Domain.Entities;
using NovaWallet.Domain.Enums;
using NovaWallet.Infrastructure.Persistence;
using Testcontainers.MsSql;

namespace NovaWallet.IntegrationTests;

public class WalletEndpointTests : IAsyncLifetime
{
    private readonly MsSqlContainer _sqlServer = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    private NovaWalletApiFactory _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await _sqlServer.StartAsync();

        _factory = new NovaWalletApiFactory(_sqlServer.GetConnectionString());
        _client = _factory.CreateClient();

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NovaWalletDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await _sqlServer.DisposeAsync();
    }

    [Fact]
    public async Task CreateWallet_CreatesWalletWithZeroBalance()
    {
        AuthenticateAsCustomer(NewCustomerId());

        var response = await _client.PostAsync("/api/wallets", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var document = await ReadJsonAsync(response);
        var data = document.RootElement.GetProperty("data");

        Assert.Equal(0, data.GetProperty("balanceKobo").GetInt64());
        Assert.Equal(CurrencyCodes.Ngn, data.GetProperty("currency").GetString());
    }

    [Fact]
    public async Task CreateWallet_WhenWalletAlreadyExists_ReturnsConflict()
    {
        var customerId = NewCustomerId();
        AuthenticateAsCustomer(customerId);

        await _client.PostAsync("/api/wallets", null);
        var response = await _client.PostAsync("/api/wallets", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task GetWallet_ReturnsCurrentCustomerWallet()
    {
        var customerId = NewCustomerId();
        AuthenticateAsCustomer(customerId);

        await _client.PostAsync("/api/wallets", null);

        var response = await _client.GetAsync("/api/wallets/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var document = await ReadJsonAsync(response);
        Assert.Equal(customerId, document.RootElement.GetProperty("data").GetProperty("customerId").GetString());
    }

    [Fact]
    public async Task GetWallet_WhenWalletDoesNotExist_ReturnsNotFound()
    {
        AuthenticateAsCustomer(NewCustomerId());

        var response = await _client.GetAsync("/api/wallets/me");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreditWallet_IncreasesBalance()
    {
        var walletId = await CreateWalletDirectlyAsync(NewCustomerId());
        AuthenticateAsCustomer("credit-actor", ApplicationRoles.NipSimulator);

        var response = await CreditAsync(walletId, 5m);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NovaWalletDbContext>();
        var wallet = await dbContext.Wallets.SingleAsync(wallet => wallet.Id == walletId);

        Assert.Equal(500, wallet.BalanceKobo);
    }

    [Fact]
    public async Task CreditWallet_CreatesTransaction()
    {
        var walletId = await CreateWalletDirectlyAsync(NewCustomerId());
        AuthenticateAsCustomer("credit-actor", ApplicationRoles.NipSimulator);

        await CreditAsync(walletId, 1m);

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NovaWalletDbContext>();
        var transaction = await dbContext.Transactions.SingleAsync(transaction => transaction.DestinationWalletId == walletId);

        Assert.Equal(TransactionType.Credit, transaction.Type);
        Assert.Equal(100, transaction.AmountKobo);
        Assert.False(string.IsNullOrWhiteSpace(transaction.Reference));
    }

    [Fact]
    public async Task CreditWallet_CreatesLedgerEntry()
    {
        var walletId = await CreateWalletDirectlyAsync(NewCustomerId());
        AuthenticateAsCustomer("credit-actor", ApplicationRoles.NipSimulator);

        await CreditAsync(walletId, 1m);

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NovaWalletDbContext>();
        var ledgerEntry = await dbContext.LedgerEntries.SingleAsync(entry => entry.WalletId == walletId);

        Assert.Equal(LedgerDirection.Credit, ledgerEntry.Direction);
        Assert.Equal(100, ledgerEntry.AmountKobo);
        Assert.Equal(100, ledgerEntry.BalanceAfterKobo);
    }

    [Fact]
    public async Task CreditWallet_CreatesAuditLog()
    {
        var walletId = await CreateWalletDirectlyAsync(NewCustomerId());
        AuthenticateAsCustomer("credit-actor", ApplicationRoles.NipSimulator);

        await CreditAsync(walletId, 1m);

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NovaWalletDbContext>();

        var auditLog = await dbContext.AuditLogs.SingleAsync(log =>
            log.WalletId == walletId &&
            log.MutationType == AuditMutationTypes.Credit);

        Assert.Equal(AuditMutationTypes.Credit, auditLog.MutationType);
        Assert.Equal(100, auditLog.AmountKobo);
        Assert.Equal(0, auditLog.BalanceBeforeKobo);
        Assert.Equal(100, auditLog.BalanceAfterKobo);
    }

    [Fact]
    public async Task CreditWallet_WhenAmountIsZero_ReturnsValidationError()
    {
        var walletId = await CreateWalletDirectlyAsync(NewCustomerId());
        AuthenticateAsCustomer("credit-actor", ApplicationRoles.NipSimulator);

        var response = await CreditAsync(walletId, 0m);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreditWallet_WhenWalletDoesNotExist_ReturnsNotFound()
    {
        AuthenticateAsCustomer("credit-actor", ApplicationRoles.NipSimulator);

        var response = await CreditAsync(Guid.NewGuid(), 1m);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreditWallet_WithoutPrivilegedRole_ReturnsForbidden()
    {
        var walletId = await CreateWalletDirectlyAsync(NewCustomerId());
        AuthenticateAsCustomer(NewCustomerId());

        var response = await CreditAsync(walletId, 1m);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ConcurrentCredits_DoNotLoseUpdates()
    {
        var walletId = await CreateWalletDirectlyAsync(NewCustomerId());

        var tasks = Enumerable.Range(0, 10).Select(_ =>
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestJwt.CreateToken("credit-actor", ApplicationRoles.NipSimulator));

            return client.PostAsJsonAsync($"/api/wallets/{walletId}/credits", new { Amount = 1m });
        });

        var responses = await Task.WhenAll(tasks);

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NovaWalletDbContext>();

        var wallet = await dbContext.Wallets.SingleAsync(wallet => wallet.Id == walletId);
        var ledgerEntryCount = await dbContext.LedgerEntries.CountAsync(entry => entry.WalletId == walletId);

        Assert.Equal(1_000, wallet.BalanceKobo);
        Assert.Equal(10, ledgerEntryCount);
    }

    [Fact]
    public async Task Transfer_IncreasesDestinationAndDecreasesSourceBalance()
    {
        var sourceCustomerId = NewCustomerId();
        var sourceWalletId = await CreateWalletDirectlyAsync(sourceCustomerId, 10_000);
        var destinationWalletId = await CreateWalletDirectlyAsync(NewCustomerId());
        AuthenticateAsCustomer(sourceCustomerId);

        var response = await TransferAsync(destinationWalletId, 25m);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var (sourceBalance, destinationBalance) = await GetBalancesAsync(sourceWalletId, destinationWalletId);
        Assert.Equal(7_500, sourceBalance);
        Assert.Equal(2_500, destinationBalance);
    }

    [Fact]
    public async Task Transfer_CreatesTransaction()
    {
        var sourceCustomerId = NewCustomerId();
        var sourceWalletId = await CreateWalletDirectlyAsync(sourceCustomerId, 10_000);
        var destinationWalletId = await CreateWalletDirectlyAsync(NewCustomerId());
        AuthenticateAsCustomer(sourceCustomerId);

        await TransferAsync(destinationWalletId, 10m);

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NovaWalletDbContext>();
        var transaction = await dbContext.Transactions.SingleAsync(transaction => transaction.Type == TransactionType.Transfer);

        Assert.Equal(sourceWalletId, transaction.SourceWalletId);
        Assert.Equal(destinationWalletId, transaction.DestinationWalletId);
        Assert.Equal(1_000, transaction.AmountKobo);
        Assert.False(string.IsNullOrWhiteSpace(transaction.Reference));
    }

    [Fact]
    public async Task Transfer_CreatesDebitAndCreditLedgerEntries()
    {
        var sourceCustomerId = NewCustomerId();
        var sourceWalletId = await CreateWalletDirectlyAsync(sourceCustomerId, 10_000);
        var destinationWalletId = await CreateWalletDirectlyAsync(NewCustomerId());
        AuthenticateAsCustomer(sourceCustomerId);

        await TransferAsync(destinationWalletId, 10m);

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NovaWalletDbContext>();
        var entries = await dbContext.LedgerEntries.OrderBy(entry => entry.Direction).ToListAsync();

        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, entry => entry.WalletId == sourceWalletId && entry.Direction == LedgerDirection.Debit && entry.AmountKobo == 1_000 && entry.BalanceAfterKobo == 9_000);
        Assert.Contains(entries, entry => entry.WalletId == destinationWalletId && entry.Direction == LedgerDirection.Credit && entry.AmountKobo == 1_000 && entry.BalanceAfterKobo == 1_000);
        Assert.Single(entries.Select(entry => entry.TransactionId).Distinct());
    }

    [Fact]
    public async Task Transfer_CreatesAuditLogsForBothWallets()
    {
        var sourceCustomerId = NewCustomerId();
        var sourceWalletId = await CreateWalletDirectlyAsync(sourceCustomerId, 10_000);
        var destinationWalletId = await CreateWalletDirectlyAsync(NewCustomerId());
        AuthenticateAsCustomer(sourceCustomerId);

        await TransferAsync(destinationWalletId, 10m);

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NovaWalletDbContext>();
        var auditLogs = await dbContext.AuditLogs.Where(log => log.TransactionId != null).ToListAsync();

        Assert.Equal(2, auditLogs.Count);
        Assert.Contains(auditLogs, log => log.WalletId == sourceWalletId && log.MutationType == AuditMutationTypes.TransferDebit && log.BalanceBeforeKobo == 10_000 && log.BalanceAfterKobo == 9_000);
        Assert.Contains(auditLogs, log => log.WalletId == destinationWalletId && log.MutationType == AuditMutationTypes.TransferCredit && log.BalanceBeforeKobo == 0 && log.BalanceAfterKobo == 1_000);
    }

    [Fact]
    public async Task Transfer_WhenFundsAreInsufficient_ReturnsAppropriateError()
    {
        var sourceCustomerId = NewCustomerId();
        var sourceWalletId = await CreateWalletDirectlyAsync(sourceCustomerId, 500);
        var destinationWalletId = await CreateWalletDirectlyAsync(NewCustomerId());
        AuthenticateAsCustomer(sourceCustomerId);

        var response = await TransferAsync(destinationWalletId, 10m);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        var (sourceBalance, destinationBalance) = await GetBalancesAsync(sourceWalletId, destinationWalletId);
        Assert.Equal(500, sourceBalance);
        Assert.Equal(0, destinationBalance);
    }

    [Fact]
    public async Task Transfer_WhenDestinationDoesNotExist_ReturnsNotFound()
    {
        var sourceCustomerId = NewCustomerId();
        await CreateWalletDirectlyAsync(sourceCustomerId, 10_000);
        AuthenticateAsCustomer(sourceCustomerId);

        var response = await TransferAsync(Guid.NewGuid(), 10m);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Transfer_ToSameWallet_IsRejected()
    {
        var sourceCustomerId = NewCustomerId();
        var sourceWalletId = await CreateWalletDirectlyAsync(sourceCustomerId, 10_000);
        AuthenticateAsCustomer(sourceCustomerId);

        var response = await TransferAsync(sourceWalletId, 10m);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Transfer_WhenDailyLimitWouldBeExceeded_IsRejected()
    {
        var sourceCustomerId = NewCustomerId();
        var sourceWalletId = await CreateWalletDirectlyAsync(sourceCustomerId, 60_000_000);
        var destinationWalletId = await CreateWalletDirectlyAsync(NewCustomerId());
        AuthenticateAsCustomer(sourceCustomerId);

        var firstResponse = await TransferAsync(destinationWalletId, 500_000m);
        var secondResponse = await TransferAsync(destinationWalletId, 1m);

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, secondResponse.StatusCode);

        var (sourceBalance, destinationBalance) = await GetBalancesAsync(sourceWalletId, destinationWalletId);
        Assert.Equal(10_000_000, sourceBalance);
        Assert.Equal(50_000_000, destinationBalance);
    }

    [Fact]
    public async Task Transfer_AtExactDailyLimit_Succeeds()
    {
        var sourceCustomerId = NewCustomerId();
        var sourceWalletId = await CreateWalletDirectlyAsync(sourceCustomerId, 50_000_000);
        var destinationWalletId = await CreateWalletDirectlyAsync(NewCustomerId());
        AuthenticateAsCustomer(sourceCustomerId);

        var response = await TransferAsync(destinationWalletId, 500_000m);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var (sourceBalance, destinationBalance) = await GetBalancesAsync(sourceWalletId, destinationWalletId);
        Assert.Equal(0, sourceBalance);
        Assert.Equal(50_000_000, destinationBalance);
    }

    [Fact]
    public async Task ConcurrentTransfers_DoNotOverspendSourceWallet()
    {
        var sourceCustomerId = NewCustomerId();
        var sourceWalletId = await CreateWalletDirectlyAsync(sourceCustomerId, 500);
        var destinationWalletId = await CreateWalletDirectlyAsync(NewCustomerId());

        var responses = await RunConcurrentTransfersAsync(sourceCustomerId, destinationWalletId, 10, 1m);
        var successfulTransfers = responses.Count(response => response.StatusCode == HttpStatusCode.OK);

        Assert.Equal(5, successfulTransfers);
        Assert.Equal(5, responses.Count(response => response.StatusCode == HttpStatusCode.UnprocessableEntity));

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NovaWalletDbContext>();
        var (sourceBalance, destinationBalance) = await GetBalancesAsync(sourceWalletId, destinationWalletId);

        Assert.Equal(0, sourceBalance);
        Assert.Equal(500, destinationBalance);
        Assert.Equal(5, await dbContext.Transactions.CountAsync(transaction => transaction.Type == TransactionType.Transfer));
        Assert.Equal(5, await dbContext.LedgerEntries.CountAsync(entry => entry.WalletId == sourceWalletId && entry.Direction == LedgerDirection.Debit));
        Assert.Equal(5, await dbContext.LedgerEntries.CountAsync(entry => entry.WalletId == destinationWalletId && entry.Direction == LedgerDirection.Credit));
    }

    [Fact]
    public async Task ConcurrentTransfers_RespectDailyOutboundLimit()
    {
        var sourceCustomerId = NewCustomerId();
        var sourceWalletId = await CreateWalletDirectlyAsync(sourceCustomerId, 100_000_000);
        var destinationWalletId = await CreateWalletDirectlyAsync(NewCustomerId());

        var responses = await RunConcurrentTransfersAsync(sourceCustomerId, destinationWalletId, 6, 100_000m);
        var successfulTransfers = responses.Count(response => response.StatusCode == HttpStatusCode.OK);

        Assert.Equal(5, successfulTransfers);

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NovaWalletDbContext>();
        var usage = await dbContext.DailyTransferLimits.SingleAsync(limit => limit.WalletId == sourceWalletId);
        var (sourceBalance, destinationBalance) = await GetBalancesAsync(sourceWalletId, destinationWalletId);

        Assert.Equal(50_000_000, usage.OutboundTotalKobo);
        Assert.Equal(50_000_000, sourceBalance);
        Assert.Equal(50_000_000, destinationBalance);
        Assert.Equal(5, await dbContext.Transactions.CountAsync(transaction => transaction.Type == TransactionType.Transfer));
    }

    [Fact]
    public async Task OppositeDirectionConcurrentTransfers_DoNotCorruptBalances()
    {
        var customerA = NewCustomerId();
        var customerB = NewCustomerId();
        var walletA = await CreateWalletDirectlyAsync(customerA, 10_000);
        var walletB = await CreateWalletDirectlyAsync(customerB, 10_000);

        var clientA = _factory.CreateClient();
        clientA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestJwt.CreateToken(customerA));

        var clientB = _factory.CreateClient();
        clientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestJwt.CreateToken(customerB));

        var responses = await Task.WhenAll(
            SendTransferAsync(clientA, walletB, 10m),
            SendTransferAsync(clientB, walletA, 10m));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));

        var (balanceA, balanceB) = await GetBalancesAsync(walletA, walletB);
        Assert.Equal(10_000, balanceA);
        Assert.Equal(10_000, balanceB);

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NovaWalletDbContext>();
        Assert.Equal(2, await dbContext.Transactions.CountAsync(transaction => transaction.Type == TransactionType.Transfer));
    }

    [Fact]
    public async Task Transfer_WithoutIdempotencyKey_ReturnsValidationError()
    {
        var sourceCustomerId = NewCustomerId();
        var sourceWalletId = await CreateWalletDirectlyAsync(sourceCustomerId, 10_000);
        var destinationWalletId = await CreateWalletDirectlyAsync(NewCustomerId());
        AuthenticateAsCustomer(sourceCustomerId);

        var response = await _client.PostAsJsonAsync("/api/wallets/transfers", new { DestinationWalletId = destinationWalletId, Amount = 10m });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var (sourceBalance, destinationBalance) = await GetBalancesAsync(sourceWalletId, destinationWalletId);
        Assert.Equal(10_000, sourceBalance);
        Assert.Equal(0, destinationBalance);
    }

    [Fact]
    public async Task Transfer_WithIdempotencyKey_CompletesSuccessfully()
    {
        var sourceCustomerId = NewCustomerId();
        await CreateWalletDirectlyAsync(sourceCustomerId, 10_000);
        var destinationWalletId = await CreateWalletDirectlyAsync(NewCustomerId());
        AuthenticateAsCustomer(sourceCustomerId);

        var response = await TransferAsync(destinationWalletId, 10m, "phase4-success");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Transfer_ReplayingSameKeyAndSamePayload_DoesNotTransferAgain()
    {
        var sourceCustomerId = NewCustomerId();
        var sourceWalletId = await CreateWalletDirectlyAsync(sourceCustomerId, 10_000);
        var destinationWalletId = await CreateWalletDirectlyAsync(NewCustomerId());
        AuthenticateAsCustomer(sourceCustomerId);

        var firstResponse = await TransferAsync(destinationWalletId, 10m, "same-key-same-payload");
        var secondResponse = await TransferAsync(destinationWalletId, 10.00m, "same-key-same-payload");

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NovaWalletDbContext>();
        var (sourceBalance, destinationBalance) = await GetBalancesAsync(sourceWalletId, destinationWalletId);

        Assert.Equal(9_000, sourceBalance);
        Assert.Equal(1_000, destinationBalance);
        Assert.Equal(1, await dbContext.Transactions.CountAsync(transaction => transaction.Type == TransactionType.Transfer));
        Assert.Equal(1, await dbContext.LedgerEntries.CountAsync(entry => entry.WalletId == sourceWalletId && entry.Direction == LedgerDirection.Debit));
        Assert.Equal(1, await dbContext.LedgerEntries.CountAsync(entry => entry.WalletId == destinationWalletId && entry.Direction == LedgerDirection.Credit));
        Assert.Equal(1_000, await dbContext.DailyTransferLimits.Where(limit => limit.WalletId == sourceWalletId).Select(limit => limit.OutboundTotalKobo).SingleAsync());
    }

    [Fact]
    public async Task Transfer_ReplayingSameKeyAndDifferentPayload_ReturnsConflict()
    {
        var sourceCustomerId = NewCustomerId();
        var sourceWalletId = await CreateWalletDirectlyAsync(sourceCustomerId, 10_000);
        var destinationWalletId = await CreateWalletDirectlyAsync(NewCustomerId());
        AuthenticateAsCustomer(sourceCustomerId);

        var firstResponse = await TransferAsync(destinationWalletId, 10m, "same-key-different-payload");
        var secondResponse = await TransferAsync(destinationWalletId, 20m, "same-key-different-payload");

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);

        var (sourceBalance, destinationBalance) = await GetBalancesAsync(sourceWalletId, destinationWalletId);
        Assert.Equal(9_000, sourceBalance);
        Assert.Equal(1_000, destinationBalance);
    }

    [Fact]
    public async Task ConcurrentTransfers_WithSameIdempotencyKey_ProcessExactlyOnce()
    {
        var sourceCustomerId = NewCustomerId();
        var sourceWalletId = await CreateWalletDirectlyAsync(sourceCustomerId, 10_000);
        var destinationWalletId = await CreateWalletDirectlyAsync(NewCustomerId());

        var responses = await RunConcurrentTransfersAsync(sourceCustomerId, destinationWalletId, 5, 10m, "concurrent-same-key");

        Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.OK);

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NovaWalletDbContext>();
        var (sourceBalance, destinationBalance) = await GetBalancesAsync(sourceWalletId, destinationWalletId);

        Assert.Equal(9_000, sourceBalance);
        Assert.Equal(1_000, destinationBalance);
        Assert.Equal(1, await dbContext.Transactions.CountAsync(transaction => transaction.Type == TransactionType.Transfer));
        Assert.Equal(2, await dbContext.LedgerEntries.CountAsync());
        Assert.Equal(2, await dbContext.AuditLogs.CountAsync(log => log.MutationType == AuditMutationTypes.TransferDebit || log.MutationType == AuditMutationTypes.TransferCredit));
        Assert.Equal(1_000, await dbContext.DailyTransferLimits.Where(limit => limit.WalletId == sourceWalletId).Select(limit => limit.OutboundTotalKobo).SingleAsync());
    }

    [Fact]
    public async Task SameIdempotencyKey_ForDifferentCustomers_DoesNotConflict()
    {
        var customerA = NewCustomerId();
        var customerB = NewCustomerId();
        await CreateWalletDirectlyAsync(customerA, 10_000);
        await CreateWalletDirectlyAsync(customerB, 10_000);
        var destinationWalletId = await CreateWalletDirectlyAsync(NewCustomerId());

        AuthenticateAsCustomer(customerA);
        var firstResponse = await TransferAsync(destinationWalletId, 10m, "shared-key");

        AuthenticateAsCustomer(customerB);
        var secondResponse = await TransferAsync(destinationWalletId, 10m, "shared-key");

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
    }

    [Fact]
    public async Task GetStatement_ReturnsWalletTransactions()
    {
        var sourceCustomerId = NewCustomerId();
        await CreateWalletDirectlyAsync(sourceCustomerId, 10_000);
        var destinationWalletId = await CreateWalletDirectlyAsync(NewCustomerId());
        AuthenticateAsCustomer(sourceCustomerId);
        await TransferAsync(destinationWalletId, 10m);

        var response = await _client.GetAsync("/api/wallets/me/statements");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await ReadJsonAsync(response);
        Assert.Equal(1, document.RootElement.GetProperty("data").GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task GetStatement_ReturnsNewestFirst()
    {
        var sourceCustomerId = NewCustomerId();
        await CreateWalletDirectlyAsync(sourceCustomerId, 10_000);
        var destinationWalletId = await CreateWalletDirectlyAsync(NewCustomerId());
        AuthenticateAsCustomer(sourceCustomerId);
        await TransferAsync(destinationWalletId, 1m);
        await TransferAsync(destinationWalletId, 2m);

        var response = await _client.GetAsync("/api/wallets/me/statements");

        var document = await ReadJsonAsync(response);
        var items = document.RootElement.GetProperty("data").GetProperty("items");
        Assert.Equal(200, items[0].GetProperty("amountKobo").GetInt64());
    }

    [Fact]
    public async Task GetStatement_IsPaginated()
    {
        var sourceCustomerId = NewCustomerId();
        await CreateWalletDirectlyAsync(sourceCustomerId, 10_000);
        var destinationWalletId = await CreateWalletDirectlyAsync(NewCustomerId());
        AuthenticateAsCustomer(sourceCustomerId);
        await TransferAsync(destinationWalletId, 1m);
        await TransferAsync(destinationWalletId, 2m);
        await TransferAsync(destinationWalletId, 3m);

        var pageOne = await ReadJsonAsync(await _client.GetAsync("/api/wallets/me/statements?page=1&pageSize=2"));
        var pageTwo = await ReadJsonAsync(await _client.GetAsync("/api/wallets/me/statements?page=2&pageSize=2"));

        Assert.Equal(2, pageOne.RootElement.GetProperty("data").GetProperty("items").GetArrayLength());
        Assert.Equal(1, pageTwo.RootElement.GetProperty("data").GetProperty("items").GetArrayLength());
        Assert.Equal(3, pageOne.RootElement.GetProperty("data").GetProperty("totalCount").GetInt32());
        Assert.Equal(2, pageOne.RootElement.GetProperty("data").GetProperty("totalPages").GetInt32());
    }

    [Fact]
    public async Task GetStatement_IncludesCreditAndDebitEntries()
    {
        var customerId = NewCustomerId();
        var walletId = await CreateWalletDirectlyAsync(customerId, 10_000);
        var destinationWalletId = await CreateWalletDirectlyAsync(NewCustomerId());

        AuthenticateAsCustomer("credit-actor", ApplicationRoles.NipSimulator);
        await CreditAsync(walletId, 1m);

        AuthenticateAsCustomer(customerId);
        await TransferAsync(destinationWalletId, 1m);

        var document = await ReadJsonAsync(await _client.GetAsync("/api/wallets/me/statements"));
        var items = document.RootElement.GetProperty("data").GetProperty("items").EnumerateArray().ToList();

        Assert.Contains(items, item => item.GetProperty("direction").GetString() == LedgerDirection.Credit.ToString());
        Assert.Contains(items, item => item.GetProperty("direction").GetString() == LedgerDirection.Debit.ToString());
    }

    [Fact]
    public async Task GetStatement_WhenWalletHasNoTransactions_ReturnsEmptyPage()
    {
        var customerId = NewCustomerId();
        await CreateWalletDirectlyAsync(customerId);
        AuthenticateAsCustomer(customerId);

        var document = await ReadJsonAsync(await _client.GetAsync("/api/wallets/me/statements"));
        var data = document.RootElement.GetProperty("data");

        Assert.Equal(0, data.GetProperty("items").GetArrayLength());
        Assert.Equal(0, data.GetProperty("totalCount").GetInt32());
        Assert.Equal(0, data.GetProperty("totalPages").GetInt32());
    }

    [Fact]
    public async Task GetStatement_WhenWalletDoesNotExist_ReturnsNotFound()
    {
        AuthenticateAsCustomer(NewCustomerId());

        var response = await _client.GetAsync("/api/wallets/me/statements");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetStatement_WithInvalidPage_ReturnsValidationError()
    {
        AuthenticateAsCustomer(NewCustomerId());

        var response = await _client.GetAsync("/api/wallets/me/statements?page=0&pageSize=20");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetStatement_WithInvalidPageSize_ReturnsValidationError()
    {
        AuthenticateAsCustomer(NewCustomerId());

        var response = await _client.GetAsync("/api/wallets/me/statements?page=1&pageSize=101");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetStatement_DoesNotExposeAnotherCustomersWalletHistory()
    {
        var customerA = NewCustomerId();
        var customerB = NewCustomerId();
        await CreateWalletDirectlyAsync(customerA, 10_000);
        var destinationWalletId = await CreateWalletDirectlyAsync(customerB);
        AuthenticateAsCustomer(customerA);
        await TransferAsync(destinationWalletId, 1m);

        AuthenticateAsCustomer(customerB);
        var document = await ReadJsonAsync(await _client.GetAsync("/api/wallets/me/statements"));
        var items = document.RootElement.GetProperty("data").GetProperty("items");

        Assert.All(items.EnumerateArray(), item => Assert.Equal(LedgerDirection.Credit.ToString(), item.GetProperty("direction").GetString()));
    }

    private async Task<Guid> CreateWalletDirectlyAsync(string customerId, long balanceKobo = 0)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NovaWalletDbContext>();

        var wallet = new Wallet
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            Currency = CurrencyCodes.Ngn,
            BalanceKobo = balanceKobo,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        dbContext.Wallets.Add(wallet);
        await dbContext.SaveChangesAsync();

        return wallet.Id;
    }

    private void AuthenticateAsCustomer(string customerId, params string[] roles)
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestJwt.CreateToken(customerId, roles));
    }

    private Task<HttpResponseMessage> CreditAsync(Guid walletId, decimal amount)
    {
        return _client.PostAsJsonAsync($"/api/wallets/{walletId}/credits", new { Amount = amount });
    }

    private Task<HttpResponseMessage> TransferAsync(Guid destinationWalletId, decimal amount, string? idempotencyKey = null)
    {
        return SendTransferAsync(_client, destinationWalletId, amount, idempotencyKey);
    }

    private Task<HttpResponseMessage[]> RunConcurrentTransfersAsync(string sourceCustomerId, Guid destinationWalletId, int requestCount, decimal amount, string? sharedIdempotencyKey = null)
    {
        var tasks = Enumerable.Range(0, requestCount).Select(_ =>
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestJwt.CreateToken(sourceCustomerId));
            return SendTransferAsync(client, destinationWalletId, amount, sharedIdempotencyKey);
        });

        return Task.WhenAll(tasks);
    }

    private static Task<HttpResponseMessage> SendTransferAsync(HttpClient client, Guid destinationWalletId, decimal amount, string? idempotencyKey = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/wallets/transfers")
        {
            Content = JsonContent.Create(new { DestinationWalletId = destinationWalletId, Amount = amount })
        };

        request.Headers.Add("Idempotency-Key", idempotencyKey ?? $"test-{Guid.NewGuid():N}");

        return client.SendAsync(request);
    }

    private async Task<(long SourceBalance, long DestinationBalance)> GetBalancesAsync(Guid sourceWalletId, Guid destinationWalletId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NovaWalletDbContext>();
        var sourceBalance = await dbContext.Wallets.Where(wallet => wallet.Id == sourceWalletId).Select(wallet => wallet.BalanceKobo).SingleAsync();
        var destinationBalance = await dbContext.Wallets.Where(wallet => wallet.Id == destinationWalletId).Select(wallet => wallet.BalanceKobo).SingleAsync();

        return (sourceBalance, destinationBalance);
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }

    private static string NewCustomerId()
    {
        return $"customer-{Guid.NewGuid():N}";
    }
}
