using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Domain.Entities.Logistics;

namespace MiniErp.Infrastructure.Persistence.Configurations;

public sealed class DriverOpeningBalanceConfiguration : AuditableEntityConfiguration<DriverOpeningBalance>
{
    public override void Configure(EntityTypeBuilder<DriverOpeningBalance> builder)
    {
        base.Configure(builder);
        builder.ToTable("DriverOpeningBalances", table =>
        {
            table.HasCheckConstraint("CK_DriverOpeningBalances_Amount_Positive", "[Amount] > 0");
            table.HasCheckConstraint("CK_DriverOpeningBalances_BalanceType", "[BalanceType] IN (1, 2)");
        });

        builder.HasKey(balance => balance.Id);
        builder.Property(balance => balance.DocumentNumber).HasMaxLength(50).IsRequired();
        builder.Property(balance => balance.DocumentDate).HasColumnType("date");
        builder.Property(balance => balance.Amount).HasPrecision(17, 2);
        builder.Property(balance => balance.Currency).HasConversion<int>();
        builder.Property(balance => balance.BalanceType).HasConversion<int>();
        builder.Property(balance => balance.Notes).HasMaxLength(1_000);
        builder.Property(balance => balance.RowVersion).IsRowVersion();

        builder.HasIndex(balance => new { balance.CompanyId, balance.FiscalYearId, balance.DriverId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(balance => new { balance.CompanyId, balance.DocumentNumber }).IsUnique();

        builder.HasOne(balance => balance.Company)
            .WithMany()
            .HasForeignKey(balance => balance.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(balance => balance.Driver)
            .WithMany()
            .HasForeignKey(balance => new { balance.CompanyId, balance.DriverId })
            .HasPrincipalKey(driver => new { driver.CompanyId, driver.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
