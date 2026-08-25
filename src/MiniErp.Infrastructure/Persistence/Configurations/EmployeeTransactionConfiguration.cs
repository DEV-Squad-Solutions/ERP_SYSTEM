using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Domain.Entities.Employees;

namespace MiniErp.Infrastructure.Persistence.Configurations;

public sealed class EmployeeTransactionConfiguration
    : AuditableEntityConfiguration<EmployeeTransaction>
{
    public override void Configure(EntityTypeBuilder<EmployeeTransaction> builder)
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

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .ValueGeneratedOnAdd();

        builder.Property(t => t.CompanyId)
            .IsRequired();

        builder.HasAlternateKey(t => new { t.CompanyId, t.Id });

        builder.Property(t => t.EmployeeId)
            .IsRequired();

        builder.Property(t => t.Type)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(t => t.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(t => t.TransactionDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(t => t.RunningBalance)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(t => t.SourceType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(t => t.SourceId);

        builder.Property(t => t.CashVoucherId)
            .IsRequired();

        builder.Property(t => t.CashBoxId)
            .IsRequired();

        builder.Property(t => t.Notes)
            .HasMaxLength(1000);

        // ── Indexes ─────────────────────────────────────────────────────────

        // Statement query: latest entries first per employee
        builder.HasIndex(t => new
        {
            t.CompanyId,
            t.EmployeeId,
            t.TransactionDate,
            t.Id
        });

        // Balance query per type
        builder.HasIndex(t => new
        {
            t.CompanyId,
            t.EmployeeId,
            t.Type
        });

        // Source document lookup (e.g., find all entries from a payroll run)
        builder.HasIndex(t => new
        {
            t.CompanyId,
            t.SourceType,
            t.SourceId
        })
            .HasFilter("[SourceId] IS NOT NULL AND [IsDeleted] = 0");

        // Cash voucher lookup
        builder.HasIndex(t => new
        {
            t.CompanyId,
            t.CashVoucherId
        });

        // Cashbox lookup
        builder.HasIndex(t => new
        {
            t.CompanyId,
            t.CashBoxId
        });

        // ── Relationships ────────────────────────────────────────────────────

        builder.HasOne(t => t.Company)
            .WithMany()
            .HasForeignKey(t => t.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Employee)
            .WithMany()
            .HasForeignKey(t => new { t.CompanyId, t.EmployeeId })
            .HasPrincipalKey(e => new { e.CompanyId, e.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.CashVoucher)
            .WithMany()
            .HasForeignKey(t => new { t.CompanyId, t.CashVoucherId })
            .HasPrincipalKey(v => new { v.CompanyId, v.Id })
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Cashbox)
            .WithMany()
            .HasForeignKey(t => new { t.CompanyId, t.CashBoxId })
            .HasPrincipalKey(c => new { c.CompanyId, c.Id })
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
    }
}