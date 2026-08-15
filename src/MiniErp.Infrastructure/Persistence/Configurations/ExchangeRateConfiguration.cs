using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Domain.Entities.Companies;

namespace MiniErp.Infrastructure.Persistence.Configurations;

public sealed class ExchangeRateConfiguration
    : AuditableEntityConfiguration<ExchangeRate>
{
    public override void Configure(EntityTypeBuilder<ExchangeRate> builder)
    {
        base.Configure(builder);

        builder.ToTable(
            "ExchangeRates",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_ExchangeRates_Rate_Positive",
                    "[Rate] > 0");
            });

        builder.HasKey(rate => rate.Id);

        builder.Property(rate => rate.Id)
            .ValueGeneratedOnAdd();

        builder.Property(rate => rate.CompanyId)
            .IsRequired();

        builder.HasAlternateKey(rate => new
        {
            rate.CompanyId,
            rate.Id
        });

        builder.Property(rate => rate.Currency)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(rate => rate.RateDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(rate => rate.Rate)
            .HasPrecision(
                ExchangeRateRules.RatePrecision,
                ExchangeRateRules.RateScale)
            .IsRequired();

        builder.Property(rate => rate.Source)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(rate => rate.Provider)
            .HasMaxLength(100);

        builder.Property(rate => rate.Notes)
            .HasMaxLength(500);

        builder.Property(rate => rate.LastModifiedAt)
            .HasColumnType("datetime2(7)")
            .IsRequired();

        builder.Property(rate => rate.RowVersion)
            .IsRowVersion()
            .IsRequired();

        builder.HasIndex(rate => new
        {
            rate.CompanyId,
            rate.Currency,
            rate.RateDate
        })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.HasIndex(rate => new
        {
            rate.CompanyId,
            rate.Currency,
            rate.RateDate,
            rate.Id
        });

        builder.HasOne(rate => rate.Company)
            .WithMany()
            .HasForeignKey(rate => rate.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(rate =>
            !rate.IsDeleted &&
            !rate.Company.IsDeleted);
    }
}
