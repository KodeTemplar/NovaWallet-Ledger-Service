using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NovaWallet.Domain.Entities;

namespace NovaWallet.Infrastructure.Persistence.Configurations;

public class DailyTransferLimitConfiguration : IEntityTypeConfiguration<DailyTransferLimit>
{
    public void Configure(EntityTypeBuilder<DailyTransferLimit> builder)
    {
        builder.ToTable("DailyTransferLimits", table =>
        {
            table.HasCheckConstraint("CK_DailyTransferLimits_OutboundTotalKobo_NonNegative", "[OutboundTotalKobo] >= 0");
        });

        builder.HasKey(limit => new { limit.WalletId, limit.WatDate });

        builder.Property(limit => limit.WatDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(limit => limit.OutboundTotalKobo)
            .HasColumnType("bigint")
            .IsRequired();

        builder.HasOne<Wallet>()
            .WithMany()
            .HasForeignKey(limit => limit.WalletId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
