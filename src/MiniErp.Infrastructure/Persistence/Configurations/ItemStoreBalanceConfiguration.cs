using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Domain.Entities.Inventory;

namespace MiniErp.Infrastructure.Persistence.Configurations;

public sealed class ItemStoreBalanceConfiguration
    : AuditableEntityConfiguration<ItemStoreBalance>
{
    public override void Configure(
        EntityTypeBuilder<ItemStoreBalance> builder)
    {
        base.Configure(builder);

        builder.ToTable(
            "ItemStoreBalances",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_ItemStoreBalances_Costs_NonNegative",
                    "[AverageCost] >= 0 AND [InventoryValue] >= 0");
                table.HasCheckConstraint(
                    "CK_ItemStoreBalances_NonPositiveState",
                    "[Quantity] > 0 OR " +
                    "([AverageCost] = 0 AND [InventoryValue] = 0)");
            });

        builder.HasKey(balance => new
        {
            balance.CompanyId,
            balance.StoreId,
            balance.ItemId
        });

        builder.Property(balance => balance.Quantity)
            .HasPrecision(
                InventoryCostRules.QuantityPrecision,
                InventoryCostRules.QuantityScale)
            .IsRequired();

        builder.Property(balance => balance.AverageCost)
            .HasPrecision(
                InventoryCostRules.UnitCostPrecision,
                InventoryCostRules.UnitCostScale)
            .IsRequired();

        builder.Property(balance => balance.InventoryValue)
            .HasPrecision(
                InventoryCostRules.ValuePrecision,
                InventoryCostRules.ValueScale)
            .IsRequired();

        builder.Property(balance => balance.RowVersion)
            .IsRowVersion()
            .IsRequired();

        builder.HasIndex(balance => new
        {
            balance.CompanyId,
            balance.ItemId
        });

        builder.HasOne(balance => balance.Company)
            .WithMany()
            .HasForeignKey(balance => balance.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(balance => balance.Store)
            .WithMany()
            .HasForeignKey(balance => new
            {
                balance.CompanyId,
                balance.StoreId
            })
            .HasPrincipalKey(store => new
            {
                store.CompanyId,
                store.Id
            })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(balance => balance.Item)
            .WithMany()
            .HasForeignKey(balance => new
            {
                balance.CompanyId,
                balance.ItemId
            })
            .HasPrincipalKey(item => new
            {
                item.CompanyId,
                item.Id
            })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
