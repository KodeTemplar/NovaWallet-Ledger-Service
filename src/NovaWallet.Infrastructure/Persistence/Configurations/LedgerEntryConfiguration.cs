using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NovaWallet.Domain.Entities;

namespace NovaWallet.Infrastructure.Persistence.Configurations;

public class LedgerEntryConfiguration : IEntityTypeConfiguration<LedgerEntry>
{
    public void Configure(EntityTypeBuilder<LedgerEntry> builder)
    {
        builder.ToTable("LedgerEntries", table =>
        {
            table.HasCheckConstraint("CK_LedgerEntries_AmountKobo_Positive", "[AmountKobo] > 0");
            table.HasCheckConstraint("CK_LedgerEntries_BalanceAfterKobo_NonNegative", "[BalanceAfterKobo] >= 0");
        });

        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.Direction)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(entry => entry.AmountKobo)
            .HasColumnType("bigint")
            .IsRequired();

        builder.Property(entry => entry.BalanceAfterKobo)
            .HasColumnType("bigint")
            .IsRequired();

        builder.Property(entry => entry.CreatedAtUtc)
            .IsRequired();

        builder.HasOne<Transaction>()
            .WithMany()
            .HasForeignKey(entry => entry.TransactionId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<Wallet>()
            .WithMany()
            .HasForeignKey(entry => entry.WalletId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(entry => entry.TransactionId);
        builder.HasIndex(entry => new { entry.WalletId, entry.CreatedAtUtc });
    }
}
