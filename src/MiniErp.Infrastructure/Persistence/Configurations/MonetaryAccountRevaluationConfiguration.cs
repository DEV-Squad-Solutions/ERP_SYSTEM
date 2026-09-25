using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Domain.Entities.Accounting;

namespace MiniErp.Infrastructure.Persistence.Configurations;

public sealed class MonetaryAccountRevaluationConfiguration
    : AuditableEntityConfiguration<MonetaryAccountRevaluation>
{
    public override void Configure(EntityTypeBuilder<MonetaryAccountRevaluation> builder)
    {
        base.Configure(builder);
        builder.ToTable("MonetaryAccountRevaluations", table =>
        {
            table.HasCheckConstraint("CK_MonetaryAccountRevaluations_Rate", "[ClosingRate] > 0");
            table.HasCheckConstraint(
                "CK_MonetaryAccountRevaluations_Party",
                "(([PartyType] IS NULL AND [PartyId] IS NULL) OR ([PartyType] IS NOT NULL AND [PartyId] IS NOT NULL))");
        });
        builder.HasKey(row => row.Id);
        builder.Property(row => row.CompanyId).IsRequired();
        builder.Property(row => row.AccountId).IsRequired();
        builder.Property(row => row.Currency).HasConversion<int>().IsRequired();
        builder.Property(row => row.PartyType).HasConversion<int>();
        builder.Property(row => row.PartyId);
        builder.Property(row => row.RevaluationDate).HasColumnType("date").IsRequired();
        builder.Property(row => row.ClosingRate).HasPrecision(28, 12).IsRequired();
        builder.Property(row => row.ForeignAmount).HasPrecision(19, 4).IsRequired();
        builder.Property(row => row.CarryingBaseAmount).HasPrecision(28, 8).IsRequired();
        builder.Property(row => row.TargetBaseAmount).HasPrecision(28, 8).IsRequired();
        builder.Property(row => row.DeltaBaseAmount).HasPrecision(28, 8).IsRequired();
        builder.Property(row => row.JournalEntryId);

        builder.HasIndex(row => new
        {
            row.CompanyId,
            row.AccountId,
            row.Currency,
            row.PartyType,
            row.PartyId,
            row.RevaluationDate
        }).IsUnique().HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_MonetaryAccountRevaluations_Target_Date");

        builder.HasOne(row => row.Company)
            .WithMany()
            .HasForeignKey(row => row.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(row => row.Account)
            .WithMany()
            .HasForeignKey(row => new { row.CompanyId, row.AccountId })
            .HasPrincipalKey(account => new { account.CompanyId, account.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(row => row.JournalEntry)
            .WithMany()
            .HasForeignKey(row => new { row.CompanyId, row.JournalEntryId })
            .HasPrincipalKey(entry => new { entry.CompanyId, entry.Id })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
