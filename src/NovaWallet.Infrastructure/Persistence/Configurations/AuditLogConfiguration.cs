using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NovaWallet.Domain.Entities;

namespace NovaWallet.Infrastructure.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs", table =>
        {
            table.HasCheckConstraint("CK_AuditLogs_AmountKobo_Positive", "[AmountKobo] >= 0");
            table.HasCheckConstraint("CK_AuditLogs_BalanceBeforeKobo_NonNegative", "[BalanceBeforeKobo] >= 0");
            table.HasCheckConstraint("CK_AuditLogs_BalanceAfterKobo_NonNegative", "[BalanceAfterKobo] >= 0");
        });

        builder.HasKey(log => log.Id);

        builder.Property(log => log.MutationType)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(log => log.AmountKobo)
            .HasColumnType("bigint")
            .IsRequired();

        builder.Property(log => log.BalanceBeforeKobo)
            .HasColumnType("bigint")
            .IsRequired();

        builder.Property(log => log.BalanceAfterKobo)
            .HasColumnType("bigint")
            .IsRequired();

        builder.Property(log => log.ActorCustomerId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(log => log.MetadataJson)
            .HasColumnType("nvarchar(max)");

        builder.Property(log => log.CreatedAtUtc)
            .IsRequired();

        builder.HasOne<Wallet>()
            .WithMany()
            .HasForeignKey(log => log.WalletId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<Transaction>()
            .WithMany()
            .HasForeignKey(log => log.TransactionId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(log => log.WalletId);
        builder.HasIndex(log => log.TransactionId);
        builder.HasIndex(log => log.CreatedAtUtc);
    }
}
