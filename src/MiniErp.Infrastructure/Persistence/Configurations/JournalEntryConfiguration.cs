using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Domain.Entities.Accounting;

namespace MiniErp.Infrastructure.Persistence.Configurations;

public sealed class JournalEntryConfiguration
    : AuditableEntityConfiguration<JournalEntry>
{
    public override void Configure(EntityTypeBuilder<JournalEntry> builder)
    {
        base.Configure(builder);

        builder.ToTable(
            "JournalEntries",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_JournalEntries_EntryType",
                    "[EntryType] IN (1, 2, 3, 4)");
                table.HasCheckConstraint(
                    "CK_JournalEntries_Source",
                    "(([EntryType] = 4 AND [SourceType] IS NOT NULL AND [SourceId] IS NOT NULL) OR " +
                    "([EntryType] = 3 AND (([SourceType] = 13 AND [SourceId] IS NOT NULL) OR ([SourceType] IS NULL AND [SourceId] IS NULL))) OR " +
                    "([EntryType] IN (1, 2) AND [SourceType] IS NULL AND [SourceId] IS NULL))");
                table.HasCheckConstraint(
                    "CK_JournalEntries_Status",
                    "[Status] IN (1, 2)");
                table.HasCheckConstraint(
                    "CK_JournalEntries_ReversalState",
                    "(([Status] = 1 AND [ReversedOn] IS NULL) OR " +
                    "([Status] = 2 AND [ReversedOn] IS NOT NULL))");
                table.HasCheckConstraint(
                    "CK_JournalEntries_NotSelfReversal",
                    "[ReversalOfEntryId] IS NULL OR [ReversalOfEntryId] <> [Id]");
            });

        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Id).ValueGeneratedOnAdd();
        builder.Property(entry => entry.CompanyId).IsRequired();
        builder.Property(entry => entry.FiscalYearId).IsRequired();
        builder.Property(entry => entry.EntryNumber)
            .HasMaxLength(50)
            .IsRequired();
        builder.Property(entry => entry.EntryDate).IsRequired();
        builder.Property(entry => entry.Description)
            .HasMaxLength(500)
            .IsRequired();
        builder.Property(entry => entry.EntryType)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(entry => entry.SourceType)
            .HasConversion<int>();
        builder.Property(entry => entry.SourceId);
        builder.Property(entry => entry.SourceNumber)
            .HasMaxLength(100);
        builder.Property(entry => entry.Status)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(entry => entry.PostedOn).IsRequired();
        builder.Property(entry => entry.RowVersion)
            .IsRowVersion()
            .IsRequired();

        builder.HasAlternateKey(entry => new
        {
            entry.CompanyId,
            entry.Id
        });

        builder.HasIndex(entry => new
        {
            entry.CompanyId,
            entry.EntryNumber
        })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_JournalEntries_Company_Number");

        builder.HasIndex(entry => new
        {
            entry.CompanyId,
            entry.FiscalYearId,
            entry.EntryDate,
            entry.EntryType,
            entry.Status
        })
            .HasDatabaseName("IX_JournalEntries_Company_FiscalYear_Date");

        builder.HasIndex(entry => new
        {
            entry.CompanyId,
            entry.ReversalOfEntryId
        })
            .IsUnique()
            .HasFilter("[ReversalOfEntryId] IS NOT NULL AND [IsDeleted] = 0")
            .HasDatabaseName("UX_JournalEntries_Company_ReversalOf");

        builder.HasIndex(entry => new
        {
            entry.CompanyId,
            entry.SourceType,
            entry.SourceId
        })
            .IsUnique()
            .HasFilter(
                "[EntryType] = 4 AND [ReversalOfEntryId] IS NULL AND " +
                "[SourceType] IS NOT NULL AND [SourceId] IS NOT NULL AND " +
                "[Status] = 1 AND [IsDeleted] = 0")
            .HasDatabaseName("UX_JournalEntries_Company_AutomaticSource");

        builder.HasIndex(entry => new
        {
            entry.CompanyId,
            entry.SourceType,
            entry.SourceId
        })
            .IsUnique()
            .HasFilter(
                "[EntryType] = 3 AND [SourceType] = 13 AND " +
                "[SourceId] IS NOT NULL AND [IsDeleted] = 0")
            .HasDatabaseName("UX_JournalEntries_Company_FiscalYearClosing");

        builder.HasOne(entry => entry.Company)
            .WithMany()
            .HasForeignKey(entry => entry.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(entry => entry.FiscalYear)
            .WithMany()
            .HasForeignKey(entry => new
            {
                entry.CompanyId,
                entry.FiscalYearId
            })
            .HasPrincipalKey(year => new
            {
                year.CompanyId,
                year.Id
            })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<JournalEntry>()
            .WithMany()
            .HasForeignKey(entry => new
            {
                entry.CompanyId,
                entry.ReversalOfEntryId
            })
            .HasPrincipalKey(entry => new
            {
                entry.CompanyId,
                entry.Id
            })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

    }
}
