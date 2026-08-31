using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Domain.Entities.Accounting;

namespace MiniErp.Infrastructure.Persistence.Configurations;

public sealed class AccountMappingConfiguration
    : AuditableEntityConfiguration<AccountMapping>
{
    public override void Configure(EntityTypeBuilder<AccountMapping> builder)
    {
        base.Configure(builder);

        builder.ToTable(
            "AccountMappings",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_AccountMappings_MappingType",
                    "[MappingType] IN (1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12)");
                table.HasCheckConstraint(
                    "CK_AccountMappings_SourceShape",
                    "(([MappingType] IN (1, 2) AND [SourceId] IS NOT NULL) OR " +
                    "([MappingType] NOT IN (1, 2) AND [SourceId] IS NULL))");
            });

        builder.HasKey(mapping => mapping.Id);
        builder.Property(mapping => mapping.Id).ValueGeneratedOnAdd();

        builder.Property(mapping => mapping.CompanyId).IsRequired();
        builder.Property(mapping => mapping.FiscalYearId).IsRequired();
        builder.Property(mapping => mapping.MappingType)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(mapping => mapping.SourceId);
        builder.Property(mapping => mapping.AccountId).IsRequired();
        builder.Property(mapping => mapping.RowVersion)
            .IsRowVersion()
            .IsRequired();

        builder.HasAlternateKey(mapping => new
        {
            mapping.CompanyId,
            mapping.Id
        });

        builder.HasIndex(mapping => new
        {
            mapping.CompanyId,
            mapping.FiscalYearId,
            mapping.MappingType,
            mapping.SourceId
        })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_AccountMappings_Scope_Type_Source");

        builder.HasIndex(mapping => new
        {
            mapping.CompanyId,
            mapping.FiscalYearId,
            mapping.AccountId
        })
            .HasDatabaseName("IX_AccountMappings_Scope_Account");

        builder.HasOne(mapping => mapping.Company)
            .WithMany()
            .HasForeignKey(mapping => mapping.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(mapping => mapping.FiscalYear)
            .WithMany()
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
            .WithMany()
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
    }
}
