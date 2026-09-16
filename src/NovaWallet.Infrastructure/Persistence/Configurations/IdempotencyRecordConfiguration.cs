using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NovaWallet.Domain.Entities;

namespace NovaWallet.Infrastructure.Persistence.Configurations;

public class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("IdempotencyRecords");

        builder.HasKey(record => record.Id);

        builder.Property(record => record.Key)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(record => record.CustomerId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(record => record.Endpoint)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(record => record.RequestHash)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(record => record.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(record => record.ResponseBodyJson)
            .HasColumnType("nvarchar(max)");

        builder.Property(record => record.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(record => new { record.CustomerId, record.Endpoint, record.Key })
            .IsUnique();

        builder.HasIndex(record => record.CreatedAtUtc);
    }
}
