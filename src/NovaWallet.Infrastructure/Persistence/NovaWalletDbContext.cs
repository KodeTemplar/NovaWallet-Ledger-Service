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

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ValidateAuditLogImmutability();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ValidateAuditLogImmutability();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NovaWalletDbContext).Assembly);
    }

    private void ValidateAuditLogImmutability()
    {
        var hasInvalidAuditLogChange = ChangeTracker.Entries<AuditLog>()
            .Any(entry => entry.State == EntityState.Modified || entry.State == EntityState.Deleted);

        if (hasInvalidAuditLogChange)
        {
            throw new InvalidOperationException("Audit log entries are immutable and cannot be modified or deleted.");
        }
    }
}
