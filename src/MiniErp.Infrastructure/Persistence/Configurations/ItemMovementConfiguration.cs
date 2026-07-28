using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Domain.Entities.Inventory;

namespace MiniErp.Infrastructure.Persistence.Configurations;

public sealed class ItemMovementConfiguration
    : AuditableEntityConfiguration<ItemMovement>
{
    public override void Configure(EntityTypeBuilder<ItemMovement> builder)
    {
        base.Configure(builder);

        builder.ToTable(
            "ItemMovements",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_ItemMovements_Quantity_NonNegative",
                    "[QuantityIn] >= 0 AND [QuantityOut] >= 0");
                table.HasCheckConstraint(
                    "CK_ItemMovements_ExactlyOneDirection",
                    "([QuantityIn] > 0 AND [QuantityOut] = 0) OR " +
                    "([QuantityIn] = 0 AND [QuantityOut] > 0)");
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

        builder.Property(movement => movement.StoreId)
            .IsRequired();

        builder.Property(movement => movement.ItemId)
            .IsRequired();

        builder.Property(movement => movement.ItemUnitId);

        builder.Property(movement => movement.MovementType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(movement => movement.ReferenceId)
            .IsRequired();

        builder.Property(movement => movement.ReferenceNumber)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(movement => movement.MovementDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(movement => movement.QuantityIn)
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(movement => movement.QuantityOut)
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(movement => movement.Description)
            .HasMaxLength(1_000);

        builder.HasIndex(movement => new
        {
            movement.CompanyId,
            movement.StoreId,
            movement.ItemId,
            movement.MovementDate,
            movement.Id
        });

        builder.HasIndex(movement => new
        {
            movement.CompanyId,
            movement.MovementType,
            movement.ReferenceId,
            movement.ItemId
        })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.HasOne(movement => movement.Company)
            .WithMany()
            .HasForeignKey(movement => movement.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(movement => movement.Store)
            .WithMany()
            .HasForeignKey(movement => new
            {
                movement.CompanyId,
                movement.StoreId
            })
            .HasPrincipalKey(store => new
            {
                store.CompanyId,
                store.Id
            })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(movement => movement.Item)
            .WithMany()
            .HasForeignKey(movement => new
            {
                movement.CompanyId,
                movement.ItemId
            })
            .HasPrincipalKey(item => new
            {
                item.CompanyId,
                item.Id
            })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(movement => movement.ItemUnit)
            .WithMany()
            .HasForeignKey(movement => new
            {
                movement.CompanyId,
                movement.ItemUnitId
            })
            .HasPrincipalKey(unit => new
            {
                unit.CompanyId,
                unit.Id
            })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
