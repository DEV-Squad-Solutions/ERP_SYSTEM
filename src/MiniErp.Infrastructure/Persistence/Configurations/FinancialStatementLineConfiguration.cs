using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Domain.Entities.Accounting;

namespace MiniErp.Infrastructure.Persistence.Configurations;

public sealed class FinancialStatementLineConfiguration
    : AuditableEntityConfiguration<FinancialStatementLine>
{
    public override void Configure(
        EntityTypeBuilder<FinancialStatementLine> builder)
    {
        base.Configure(builder);

        builder.ToTable(
            "FinancialStatementLines",
            table => table.HasCheckConstraint(
                "CK_FinancialStatementLines_StatementType",
                "[StatementType] IN (1, 2, 3)"));

        builder.HasKey(line => line.Id);
        builder.Property(line => line.Id).ValueGeneratedOnAdd();
        builder.Property(line => line.CompanyId).IsRequired();
        builder.Property(line => line.FiscalYearId).IsRequired();

        builder.HasAlternateKey(line => new
        {
            line.CompanyId,
            line.FiscalYearId,
            line.StatementType,
            line.Id
        });

        builder.Property(line => line.StatementType)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(line => line.Code)
            .HasMaxLength(50)
            .IsRequired();
        builder.Property(line => line.Name)
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(line => line.DisplayOrder).IsRequired();
        builder.Property(line => line.IsAssignable)
            .HasDefaultValue(false)
            .IsRequired();
        builder.Property(line => line.IsActive)
            .HasDefaultValue(true)
            .IsRequired();
        builder.Property(line => line.RowVersion)
            .IsRowVersion()
            .IsRequired();

        builder.HasIndex(line => new
        {
            line.CompanyId,
            line.FiscalYearId,
            line.StatementType,
            line.Code
        })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_FinancialStatementLines_Scope_Code");

        builder.HasIndex(line => new
        {
            line.CompanyId,
            line.FiscalYearId,
            line.StatementType,
            line.ParentLineId,
            line.DisplayOrder,
            line.Id
        })
            .HasDatabaseName("IX_FinancialStatementLines_Hierarchy");

        builder.HasOne(line => line.Company)
            .WithMany(company => company.FinancialStatementLines)
            .HasForeignKey(line => line.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(line => line.FiscalYear)
            .WithMany(fiscalYear => fiscalYear.FinancialStatementLines)
            .HasForeignKey(line => new
            {
                line.CompanyId,
                line.FiscalYearId
            })
            .HasPrincipalKey(fiscalYear => new
            {
                fiscalYear.CompanyId,
                fiscalYear.Id
            })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(line => line.ParentLine)
            .WithMany(line => line.Children)
            .HasForeignKey(line => new
            {
                line.CompanyId,
                line.FiscalYearId,
                line.StatementType,
                line.ParentLineId
            })
            .HasPrincipalKey(line => new
            {
                line.CompanyId,
                line.FiscalYearId,
                line.StatementType,
                line.Id
            })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
