using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Domain.Entities.Employees;

namespace MiniErp.Infrastructure.Persistence.Configurations;

public sealed class EmployeeTransactionConfiguration
    : AuditableEntityConfiguration<EmployeeTransaction>
{
    public override void Configure(
        EntityTypeBuilder<EmployeeTransaction> builder)
    {
        base.Configure(builder);

        builder.ToTable(
            "EmployeeTransactions",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_EmployeeTransactions_Amount_Positive",
                    "[Amount] > 0");
            });

        builder.HasKey(transaction => transaction.Id);

        builder.Property(transaction => transaction.Id)
            .ValueGeneratedOnAdd();

        builder.Property(transaction => transaction.CompanyId)
            .IsRequired();

        builder.HasAlternateKey(transaction => new
        {
            transaction.CompanyId,
            transaction.Id
        });

        builder.Property(transaction => transaction.EmployeeId)
            .IsRequired();

        builder.Property(transaction => transaction.Type)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(transaction => transaction.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(transaction => transaction.TransactionDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(transaction => transaction.IsProcessed)
            .IsRequired();

        builder.Property(transaction => transaction.PayrollEntryId);

        builder.Property(transaction => transaction.Notes)
            .HasMaxLength(1000);

        builder.HasIndex(transaction => new
        {
            transaction.CompanyId,
            transaction.EmployeeId,
            transaction.TransactionDate,
            transaction.Id
        });

        builder.HasIndex(transaction => new
        {
            transaction.CompanyId,
            transaction.EmployeeId,
            transaction.Type,
            transaction.IsProcessed
        });

        builder.HasIndex(transaction => new
        {
            transaction.CompanyId,
            transaction.PayrollEntryId
        })
            .HasFilter("[PayrollEntryId] IS NOT NULL AND [IsDeleted] = 0");

        builder.HasOne(transaction => transaction.Company)
            .WithMany()
            .HasForeignKey(transaction => transaction.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(transaction => transaction.Employee)
            .WithMany()
            .HasForeignKey(transaction => new
            {
                transaction.CompanyId,
                transaction.EmployeeId
            })
            .HasPrincipalKey(employee => new
            {
                employee.CompanyId,
                employee.Id
            })
            .OnDelete(DeleteBehavior.Restrict);
    }
}