using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Domain.Entities.Accounting;

namespace MiniErp.Infrastructure.Persistence.Configurations;

public sealed class AccountConfiguration
    : AuditableEntityConfiguration<Account>
{
    public override void Configure(EntityTypeBuilder<Account> builder)
    {
        base.Configure(builder);

        builder.ToTable(
            "Accounts",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_Accounts_AccountType",
                    "[AccountType] IN (1, 2, 3, 4, 5)");
                table.HasCheckConstraint(
                    "CK_Accounts_NormalBalance",
                    "[NormalBalance] IN (1, 2)");
            });

        builder.HasKey(account => account.Id);
        builder.Property(account => account.Id).ValueGeneratedOnAdd();
        builder.Property(account => account.CompanyId).IsRequired();

        builder.HasAlternateKey(account => new
        {
            account.CompanyId,
            account.Id
        });

        builder.Property(account => account.Code)
            .HasMaxLength(50)
            .IsRequired();
        builder.Property(account => account.Name)
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(account => account.AccountType)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(account => account.NormalBalance)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(account => account.IsPosting)
            .HasDefaultValue(false)
            .IsRequired();
        builder.Property(account => account.IsActive)
            .HasDefaultValue(true)
            .IsRequired();
        builder.Property(account => account.RowVersion)
            .IsRowVersion()
            .IsRequired();

        builder.HasIndex(account => new
        {
            account.CompanyId,
            account.Code
        })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_Accounts_Company_Code");

        builder.HasIndex(account => new
        {
            account.CompanyId,
            account.ParentAccountId,
            account.AccountType,
            account.IsActive,
            account.Code
        })
            .HasDatabaseName("IX_Accounts_Company_Hierarchy");

        builder.HasOne(account => account.Company)
            .WithMany(company => company.Accounts)
            .HasForeignKey(account => account.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(account => account.ParentAccount)
            .WithMany(account => account.Children)
            .HasForeignKey(account => new
            {
                account.CompanyId,
                account.ParentAccountId
            })
            .HasPrincipalKey(account => new
            {
                account.CompanyId,
                account.Id
            })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
