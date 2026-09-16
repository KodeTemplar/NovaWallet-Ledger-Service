using Microsoft.EntityFrameworkCore;
using NovaWallet.Domain.Entities;

namespace NovaWallet.Infrastructure.Persistence;

public class NovaWalletDbContext(DbContextOptions<NovaWalletDbContext> options) : DbContext(options)
{
    public DbSet<Wallet> Wallets => Set<Wallet>();

    public DbSet<Transaction> Transactions => Set<Transaction>();

    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();

    public DbSet<DailyTransferLimit> DailyTransferLimits => Set<DailyTransferLimit>();

    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<FinancialLimit> FinancialLimits => Set<FinancialLimit>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NovaWalletDbContext).Assembly);
    }
}
