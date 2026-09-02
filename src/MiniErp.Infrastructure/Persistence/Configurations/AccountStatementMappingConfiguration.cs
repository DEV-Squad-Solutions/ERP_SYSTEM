using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Domain.Entities.Accounting;

namespace MiniErp.Infrastructure.Persistence.Configurations;

public sealed class AccountStatementMappingConfiguration
    : AuditableEntityConfiguration<AccountStatementMapping>
{
    public override void Configure(
        EntityTypeBuilder<AccountStatementMapping> builder)
    {
        base.Configure(builder);

        builder.ToTable(
            "AccountStatementMappings",
            table => table.HasCheckConstraint(
                "CK_AccountStatementMappings_StatementType",
                "[StatementType] IN (1, 2, 3)"));

        builder.HasKey(mapping => mapping.Id);
        builder.Property(mapping => mapping.Id).ValueGeneratedOnAdd();
        builder.Property(mapping => mapping.CompanyId).IsRequired();
        builder.Property(mapping => mapping.FiscalYearId).IsRequired();
        builder.Property(mapping => mapping.StatementType)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(mapping => mapping.AccountId).IsRequired();
        builder.Property(mapping => mapping.FinancialStatementLineId)
            .IsRequired();

        builder.HasIndex(mapping => new
        {
            mapping.CompanyId,
            mapping.FiscalYearId,
            mapping.StatementType,
            mapping.AccountId
        })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_AccountStatementMappings_Scope_Account");

        builder.HasIndex(mapping => new
        {
            mapping.CompanyId,
            mapping.FiscalYearId,
            mapping.StatementType,
            mapping.FinancialStatementLineId
        })
            .HasDatabaseName("IX_AccountStatementMappings_Line");

        builder.HasOne(mapping => mapping.Company)
            .WithMany(company => company.AccountStatementMappings)
            .HasForeignKey(mapping => mapping.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(mapping => mapping.FiscalYear)
            .WithMany(fiscalYear => fiscalYear.AccountStatementMappings)
            .HasForeignKey(mapping => new
            {
                mapping.CompanyId,
                mapping.FiscalYearId
            })
            .HasPrincipalKey(fiscalYear => new
            {
                fiscalYear.CompanyId,
                fiscalYear.Id
            })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(mapping => mapping.Account)
            .WithMany(account => account.StatementMappings)
            .HasForeignKey(mapping => new
            {
                mapping.CompanyId,
                mapping.AccountId
            })
            .HasPrincipalKey(account => new
            {
                account.CompanyId,
                account.Id
            })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(mapping => mapping.FinancialStatementLine)
            .WithMany(line => line.AccountMappings)
            .HasForeignKey(mapping => new
            {
                mapping.CompanyId,
                mapping.FiscalYearId,
                mapping.StatementType,
                mapping.FinancialStatementLineId
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
