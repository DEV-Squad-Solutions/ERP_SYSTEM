using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Domain.Entities.Accounting;

namespace MiniErp.Infrastructure.Persistence.Configurations;

public sealed class JournalEntryLineConfiguration
    : AuditableEntityConfiguration<JournalEntryLine>
{
    public override void Configure(EntityTypeBuilder<JournalEntryLine> builder)
    {
        base.Configure(builder);

        builder.ToTable(
            "JournalEntryLines",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_JournalEntryLines_DebitCredit",
                    "(([Debit] > 0 AND [Credit] = 0) OR " +
                    "([Credit] > 0 AND [Debit] = 0))");
                table.HasCheckConstraint(
                    "CK_JournalEntryLines_PartyShape",
                    "(([PartyType] IS NULL AND [PartyId] IS NULL) OR " +
                    "([PartyType] IN (1, 2, 3, 4, 5) AND [PartyId] IS NOT NULL))");
            });

        builder.HasKey(line => line.Id);
        builder.Property(line => line.Id).ValueGeneratedOnAdd();
        builder.Property(line => line.CompanyId).IsRequired();
        builder.Property(line => line.JournalEntryId).IsRequired();
        builder.Property(line => line.AccountId).IsRequired();
        builder.Property(line => line.PartyType).HasConversion<int>();
        builder.Property(line => line.PartyId);
        builder.Property(line => line.Description).HasMaxLength(300);
        builder.Property(line => line.Debit)
            .HasPrecision(19, 4)
            .IsRequired();
        builder.Property(line => line.Credit)
            .HasPrecision(19, 4)
            .IsRequired();

        builder.HasIndex(line => new
        {
            line.CompanyId,
            line.JournalEntryId,
            line.Id
        })
            .HasDatabaseName("IX_JournalEntryLines_Company_Entry");

        builder.HasIndex(line => new
        {
            line.CompanyId,
            line.AccountId,
            line.JournalEntryId
        })
            .HasDatabaseName("IX_JournalEntryLines_Company_Account");

        builder.HasIndex(line => new
        {
            line.CompanyId,
            line.PartyType,
            line.PartyId,
            line.JournalEntryId
        })
            .HasFilter("[PartyType] IS NOT NULL AND [PartyId] IS NOT NULL")
            .HasDatabaseName("IX_JournalEntryLines_Company_Party");

        builder.HasOne(line => line.Company)
            .WithMany()
            .HasForeignKey(line => line.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(line => line.JournalEntry)
            .WithMany(entry => entry.Lines)
            .HasForeignKey(line => new
            {
                line.CompanyId,
                line.JournalEntryId
            })
            .HasPrincipalKey(entry => new
            {
                entry.CompanyId,
                entry.Id
            })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(line => line.Account)
            .WithMany()
            .HasForeignKey(line => new
            {
                line.CompanyId,
                line.AccountId
            })
            .HasPrincipalKey(account => new
            {
                account.CompanyId,
                account.Id
            })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
