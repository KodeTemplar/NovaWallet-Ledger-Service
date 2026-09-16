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

    private async Task<Guid> CreateWalletDirectlyAsync(string customerId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NovaWalletDbContext>();

        var wallet = new Wallet
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            Currency = CurrencyCodes.Ngn,
            BalanceKobo = 0,
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