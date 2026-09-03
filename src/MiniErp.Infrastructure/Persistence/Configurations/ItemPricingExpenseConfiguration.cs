using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Domain.Entities.Catalog;
using MiniErp.Domain.Entities.Inventory;

namespace MiniErp.Infrastructure.Persistence.Configurations;

public sealed class ItemPricingExpenseConfiguration
    : AuditableEntityConfiguration<ItemPricingExpense>
{
    public override void Configure(EntityTypeBuilder<ItemPricingExpense> builder)
    {
        base.Configure(builder);

        builder.ToTable(
            "ItemPricingExpenses",
            table => table.HasCheckConstraint(
                "CK_ItemPricingExpenses_Amount_Positive",
                "[Amount] > 0"));

        builder.HasKey(expense => expense.Id);
        builder.Property(expense => expense.Id).ValueGeneratedOnAdd();
        builder.Property(expense => expense.CompanyId).IsRequired();
        builder.Property(expense => expense.ItemId).IsRequired();
        builder.Property(expense => expense.Name).HasMaxLength(200).IsRequired();
        builder.Property(expense => expense.Amount)
            .HasPrecision(InventoryCostRules.ValuePrecision, InventoryCostRules.ValueScale)
            .IsRequired();
        builder.Property(expense => expense.Notes).HasMaxLength(1_000);

        builder.HasOne(expense => expense.Company)
            .WithMany()
            .HasForeignKey(expense => expense.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(expense => expense.Item)
            .WithMany(item => item.PricingExpenses)
            .HasForeignKey(expense => new { expense.CompanyId, expense.ItemId })
            .HasPrincipalKey(item => new { item.CompanyId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(expense => new
            {
                expense.CompanyId,
                expense.ItemId,
                expense.Name
            })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
    }
}
