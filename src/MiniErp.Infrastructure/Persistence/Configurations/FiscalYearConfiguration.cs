using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Domain.Entities.Accounting;

namespace MiniErp.Infrastructure.Persistence.Configurations;

public sealed class FiscalYearConfiguration
    : AuditableEntityConfiguration<FiscalYear>
{
    public override void Configure(EntityTypeBuilder<FiscalYear> builder)
    {
        base.Configure(builder);

        builder.ToTable(
            "FiscalYears",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_FiscalYears_DateRange",
                    "[StartDate] < [EndDate]");
                table.HasCheckConstraint(
                    "CK_FiscalYears_Status",
                    "[Status] IN (1, 2)");
            });

        builder.HasKey(fiscalYear => fiscalYear.Id);

        builder.Property(fiscalYear => fiscalYear.Id)
            .ValueGeneratedOnAdd();

        builder.Property(fiscalYear => fiscalYear.CompanyId)
            .IsRequired();

        builder.HasAlternateKey(fiscalYear => new
        {
            fiscalYear.CompanyId,
            fiscalYear.Id
        });

        builder.Property(fiscalYear => fiscalYear.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(fiscalYear => fiscalYear.StartDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(fiscalYear => fiscalYear.EndDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(fiscalYear => fiscalYear.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(fiscalYear => fiscalYear.IsCurrent)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(fiscalYear => fiscalYear.ClosedOn)
            .HasColumnType("datetime2(7)");

        builder.Property(fiscalYear => fiscalYear.RowVersion)
            .IsRowVersion()
            .IsRequired();

        builder.HasIndex(fiscalYear => new
        {
            fiscalYear.CompanyId,
            fiscalYear.Name
        })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_FiscalYears_Company_Name");

        builder.HasIndex(fiscalYear => new
        {
            fiscalYear.CompanyId,
            fiscalYear.StartDate,
            fiscalYear.EndDate,
            fiscalYear.Id
        })
            .HasDatabaseName("IX_FiscalYears_Company_DateRange");

        builder.HasIndex(fiscalYear => new
        {
            fiscalYear.CompanyId,
            fiscalYear.IsCurrent
        })
            .IsUnique()
            .HasFilter("[IsCurrent] = 1 AND [IsDeleted] = 0")
            .HasDatabaseName("UX_FiscalYears_Company_Current");

        builder.HasOne(fiscalYear => fiscalYear.Company)
            .WithMany(company => company.FiscalYears)
            .HasForeignKey(fiscalYear => fiscalYear.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
