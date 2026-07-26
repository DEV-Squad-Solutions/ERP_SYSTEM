using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Domain.Entities.Containers;

namespace MiniErp.Infrastructure.Persistence.Configurations;

public sealed class ContainerMovementConfiguration
    : AuditableEntityConfiguration<ContainerMovement>
{
    public override void Configure(EntityTypeBuilder<ContainerMovement> builder)
    {
        base.Configure(builder);

        builder.ToTable(
            "ContainerMovements",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_ContainerMovements_Units_NonNegative",
                    "[OutgoingUnits] >= 0 AND [IncomingUnits] >= 0");
                table.HasCheckConstraint(
                    "CK_ContainerMovements_Units_NotBothZero",
                    "[OutgoingUnits] > 0 OR [IncomingUnits] > 0");
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

        builder.Property(movement => movement.ContainerStoreId)
            .IsRequired();

        builder.Property(movement => movement.ContainerId)
            .IsRequired();

        builder.Property(movement => movement.InvoiceId)
            .IsRequired();

        builder.Property(movement => movement.InvoiceNumber)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(movement => movement.MovementDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(movement => movement.OutgoingUnits)
            .IsRequired();

        builder.Property(movement => movement.IncomingUnits)
            .IsRequired();

        builder.Property(movement => movement.Description)
            .HasMaxLength(1_000);

        builder.HasIndex(movement => new
        {
            movement.CompanyId,
            movement.InvoiceId,
            movement.ContainerId
        })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.HasIndex(movement => new
        {
            movement.CompanyId,
            movement.BusinessPartnerId,
            movement.ContainerId,
            movement.MovementDate
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

        builder.HasOne(movement => movement.ContainerStore)
            .WithMany()
            .HasForeignKey(movement => new
            {
                movement.CompanyId,
                movement.ContainerStoreId
            })
            .HasPrincipalKey(store => new
            {
                store.CompanyId,
                store.Id
            })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(movement => movement.Container)
            .WithMany()
            .HasForeignKey(movement => new
            {
                movement.CompanyId,
                movement.ContainerId
            })
            .HasPrincipalKey(container => new
            {
                container.CompanyId,
                container.Id
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
