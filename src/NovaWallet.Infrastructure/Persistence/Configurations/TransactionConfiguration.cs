using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NovaWallet.Domain.Entities;

namespace NovaWallet.Infrastructure.Persistence.Configurations;

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("Transactions", table =>
        {
            table.HasCheckConstraint("CK_Transactions_AmountKobo_Positive", "[AmountKobo] > 0");
        });

        builder.HasKey(transaction => transaction.Id);

        builder.Property(transaction => transaction.Type)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(transaction => transaction.AmountKobo)
            .HasColumnType("bigint")
            .IsRequired();

        builder.Property(transaction => transaction.Reference)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(transaction => transaction.CreatedAtUtc)
            .IsRequired();

        builder.HasOne<Wallet>()
            .WithMany()
            .HasForeignKey(transaction => transaction.SourceWalletId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<Wallet>()
            .WithMany()
            .HasForeignKey(transaction => transaction.DestinationWalletId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(transaction => transaction.Reference);
        builder.HasIndex(transaction => transaction.CreatedAtUtc);
        builder.HasIndex(transaction => new { transaction.SourceWalletId, transaction.CreatedAtUtc });
        builder.HasIndex(transaction => new { transaction.DestinationWalletId, transaction.CreatedAtUtc });
    }
}
