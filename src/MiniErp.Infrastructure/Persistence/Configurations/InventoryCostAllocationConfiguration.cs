using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Domain.Entities.Inventory;

namespace MiniErp.Infrastructure.Persistence.Configurations;

public sealed class InventoryCostAllocationConfiguration
    : IEntityTypeConfiguration<InventoryCostAllocation>
{
    public void Configure(
        EntityTypeBuilder<InventoryCostAllocation> builder)
    {
        builder.ToTable(
            "InventoryCostAllocations",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_InventoryCostAllocations_Quantity_Positive",
                    "[Quantity] > 0");
                table.HasCheckConstraint(
                    "CK_InventoryCostAllocations_Cost_NonNegative",
                    "[UnitCost] >= 0 AND [TotalCost] >= 0");
                table.HasCheckConstraint(
                    "CK_InventoryCostAllocations_DifferentMovements",
                    "[OutboundMovementId] <> [InboundMovementId]");
            });

        builder.HasKey(allocation => allocation.Id);

        builder.Property(allocation => allocation.Id)
            .ValueGeneratedOnAdd();

        builder.Property(allocation => allocation.CompanyId)
            .IsRequired();

        builder.Property(allocation => allocation.StoreId)
            .IsRequired();

        builder.Property(allocation => allocation.ItemId)
            .IsRequired();

        builder.Property(allocation => allocation.OutboundMovementId)
            .IsRequired();

        builder.Property(allocation => allocation.InboundMovementId)
            .IsRequired();

        builder.Property(allocation => allocation.Quantity)
            .HasPrecision(
                InventoryCostRules.QuantityPrecision,
                InventoryCostRules.QuantityScale)
            .IsRequired();

        builder.Property(allocation => allocation.UnitCost)
            .HasPrecision(
                InventoryCostRules.UnitCostPrecision,
                InventoryCostRules.UnitCostScale)
            .IsRequired();

        builder.Property(allocation => allocation.TotalCost)
            .HasPrecision(
                InventoryCostRules.ValuePrecision,
                InventoryCostRules.ValueScale)
            .IsRequired();

        builder.Property(allocation => allocation.CreatedOn)
            .HasColumnType("datetime2(7)")
            .IsRequired();

        builder.HasIndex(allocation => new
        {
            allocation.CompanyId,
            allocation.OutboundMovementId,
            allocation.InboundMovementId
        })
            .IsUnique();

        builder.HasIndex(allocation => new
        {
            allocation.CompanyId,
            allocation.StoreId,
            allocation.ItemId,
            allocation.OutboundMovementId
        });

        builder.HasIndex(allocation => new
        {
            allocation.CompanyId,
            allocation.StoreId,
            allocation.ItemId,
            allocation.InboundMovementId
        });

        builder.HasOne(allocation => allocation.Company)
            .WithMany()
            .HasForeignKey(allocation => allocation.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(allocation => allocation.Store)
            .WithMany()
            .HasForeignKey(allocation => new
            {
                allocation.CompanyId,
                allocation.StoreId
            })
            .HasPrincipalKey(store => new
            {
                store.CompanyId,
                store.Id
            })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(allocation => allocation.Item)
            .WithMany()
            .HasForeignKey(allocation => new
            {
                allocation.CompanyId,
                allocation.ItemId
            })
            .HasPrincipalKey(item => new
            {
                item.CompanyId,
                item.Id
            })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(allocation => allocation.OutboundMovement)
            .WithMany(movement => movement.OutboundCostAllocations)
            .HasForeignKey(allocation => new
            {
                allocation.CompanyId,
                allocation.OutboundMovementId
            })
            .HasPrincipalKey(movement => new
            {
                movement.CompanyId,
                movement.Id
            })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(allocation => allocation.InboundMovement)
            .WithMany(movement => movement.InboundCostAllocations)
            .HasForeignKey(allocation => new
            {
                allocation.CompanyId,
                allocation.InboundMovementId
            })
            .HasPrincipalKey(movement => new
            {
                movement.CompanyId,
                movement.Id
            })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
