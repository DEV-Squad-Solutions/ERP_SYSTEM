using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Domain.Entities.BusinessPartners;

namespace MiniErp.Infrastructure.Persistence.Configurations;

public sealed class BusinessPartnerMovementConfiguration
    : AuditableEntityConfiguration<BusinessPartnerMovement>
{
    public override void Configure(
        EntityTypeBuilder<BusinessPartnerMovement> builder)
    {
        base.Configure(builder);

        builder.ToTable(
            "BusinessPartnerMovements",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_BusinessPartnerMovements_Amounts_NonNegative",
                    "[Debit] >= 0 AND [Credit] >= 0");
                table.HasCheckConstraint(
                    "CK_BusinessPartnerMovements_ExactlyOneAmount",
                    "([Debit] > 0 AND [Credit] = 0) OR " +
                    "([Debit] = 0 AND [Credit] > 0)");
            });
        builder.HasKey(movement => movement.Id);

        builder.Property(movement => movement.Id)
            .ValueGeneratedOnAdd();

        builder.Property(movement => movement.CompanyId)
            .IsRequired();

        builder.HasAlternateKey(movement => new
        {
            movement.CompanyId,
            movement.Id
        });

        builder.Property(movement => movement.BusinessPartnerId)
            .IsRequired();

        builder.Property(movement => movement.InvoiceId)
            .IsRequired();

        builder.Property(movement => movement.MovementType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(movement => movement.MovementDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(movement => movement.Currency)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(movement => movement.Debit)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(movement => movement.Credit)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(movement => movement.Description)
            .HasMaxLength(1_000);

        builder.HasIndex(movement => new
        {
            movement.CompanyId,
            movement.InvoiceId
        })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.HasIndex(movement => new
        {
            movement.CompanyId,
            movement.BusinessPartnerId,
            movement.Currency,
            movement.MovementDate,
            movement.Id
        });

        builder.HasOne(movement => movement.Company)
            .WithMany()
            .HasForeignKey(movement => movement.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(movement => movement.BusinessPartner)
            .WithMany()
            .HasForeignKey(movement => new
            {
                movement.CompanyId,
                movement.BusinessPartnerId
            })
            .HasPrincipalKey(partner => new
            {
                partner.CompanyId,
                partner.Id
            })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(movement => movement.Invoice)
            .WithMany()
            .HasForeignKey(movement => new
            {
                movement.CompanyId,
                movement.InvoiceId
            })
            .HasPrincipalKey(invoice => new
            {
                invoice.CompanyId,
                invoice.Id
            })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
