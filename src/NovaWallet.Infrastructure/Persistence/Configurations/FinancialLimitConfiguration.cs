using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NovaWallet.Domain.Constants;
using NovaWallet.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NovaWallet.Infrastructure.Persistence.Configurations
{
    public class FinancialLimitConfiguration : IEntityTypeConfiguration<FinancialLimit>
    {
        public void Configure(EntityTypeBuilder<FinancialLimit> builder)
        {
            builder.ToTable("FinancialLimits");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Code)
                .HasMaxLength(100)
                .IsRequired();

            builder.HasIndex(x => x.Code)
                .IsUnique();

            builder.Property(x => x.AmountKobo)
                .IsRequired();

            builder.Property(x => x.IsActive)
                .IsRequired();

            builder.ToTable(table =>
                table.HasCheckConstraint(
                    "CK_FinancialLimits_AmountKobo_Positive",
                    "[AmountKobo] > 0"));

            builder.HasData(
                new FinancialLimit
                {
                    Id = 1,
                    Code = FinancialLimitCodes.DailyOutboundTransfer,
                    AmountKobo = 50_000_000,
                    IsActive = true
                });
        }
    }
}
