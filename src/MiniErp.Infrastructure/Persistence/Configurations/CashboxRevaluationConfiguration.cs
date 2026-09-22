using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Domain.Entities.CashManagement;

namespace MiniErp.Infrastructure.Persistence.Configurations;

public sealed class CashboxRevaluationConfiguration
    : AuditableEntityConfiguration<CashboxRevaluation>
{
    public override void Configure(EntityTypeBuilder<CashboxRevaluation> builder)
    {
        base.Configure(builder);
        builder.ToTable("CashboxRevaluations", table =>
        {
            table.HasCheckConstraint("CK_CashboxRevaluations_Rate", "[ClosingRate] > 0");
        });
        builder.HasKey(row => row.Id);
        builder.Property(row => row.Id).ValueGeneratedOnAdd();
        builder.Property(row => row.CompanyId).IsRequired();
        builder.Property(row => row.CashboxId).IsRequired();
        builder.Property(row => row.RevaluationDate).HasColumnType("date").IsRequired();
        builder.Property(row => row.ClosingRate).HasPrecision(28, 12).IsRequired();
        builder.Property(row => row.ForeignAmount).HasPrecision(19, 4).IsRequired();
        builder.Property(row => row.CarryingBaseAmount).HasPrecision(28, 8).IsRequired();
        builder.Property(row => row.TargetBaseAmount).HasPrecision(28, 8).IsRequired();
        builder.Property(row => row.DeltaBaseAmount).HasPrecision(28, 8).IsRequired();
        builder.Property(row => row.JournalEntryId);
        builder.HasIndex(row => new { row.CompanyId, row.CashboxId, row.RevaluationDate })
            .IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasOne(row => row.Company).WithMany().HasForeignKey(row => row.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(row => row.Cashbox).WithMany().HasForeignKey(row => new { row.CompanyId, row.CashboxId })
            .HasPrincipalKey(cashbox => new { cashbox.CompanyId, cashbox.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(row => row.JournalEntry).WithMany().HasForeignKey(row => new { row.CompanyId, row.JournalEntryId })
            .HasPrincipalKey(entry => new { entry.CompanyId, entry.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
