using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NovaWallet.Domain.Entities;

namespace NovaWallet.Infrastructure.Persistence.Configurations;

public class WalletConfiguration : IEntityTypeConfiguration<Wallet>
{
    public void Configure(EntityTypeBuilder<Wallet> builder)
    {
        builder.ToTable("Wallets", table =>
        {
            table.HasCheckConstraint("CK_Wallets_BalanceKobo_NonNegative", "[BalanceKobo] >= 0");
        });

        builder.HasKey(wallet => wallet.Id);

        builder.Property(wallet => wallet.CustomerId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(wallet => wallet.Currency)
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(wallet => wallet.BalanceKobo)
            .HasColumnType("bigint")
            .IsRequired();

        builder.Property(wallet => wallet.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(wallet => wallet.CustomerId)
            .IsUnique();
    }
}
