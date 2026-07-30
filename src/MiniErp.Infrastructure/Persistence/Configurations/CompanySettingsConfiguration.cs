using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Domain.Entities.Companies;

namespace MiniErp.Infrastructure.Persistence.Configurations;

public sealed class CompanySettingsConfiguration
    : IEntityTypeConfiguration<CompanySettings>
{
    public void Configure(EntityTypeBuilder<CompanySettings> builder)
    {
        builder.ToTable("CompanySettings");
        builder.HasKey(settings => settings.CompanyId);

        builder.Property(settings => settings.BaseCurrency)
            .HasConversion<int>()
            .HasDefaultValue(Domain.Enums.CurrencyCode.EGP)
            .IsRequired();

        builder.Property(settings => settings.StockBalanceCheckMode)
            .HasConversion<int>()
            .IsRequired();

        builder.HasOne(settings => settings.Company)
            .WithOne(company => company.Settings)
            .HasForeignKey<CompanySettings>(settings => settings.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(settings => !settings.Company.IsDeleted);
    }
}
