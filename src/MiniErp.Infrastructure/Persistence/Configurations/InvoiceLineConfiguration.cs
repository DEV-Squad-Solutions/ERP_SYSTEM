using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Domain.Entities.Inventory;
using MiniErp.Domain.Entities.Invoicing;

namespace MiniErp.Infrastructure.Persistence.Configurations;

public sealed class InvoiceLineConfiguration
    : AuditableEntityConfiguration<InvoiceLine>
{
    public override void Configure(EntityTypeBuilder<InvoiceLine> builder)
    {
        base.Configure(builder);

        builder.ToTable(
            "InvoiceLines",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_InvoiceLines_Count_Positive",
                    "[Count] > 0");
                table.HasCheckConstraint(
                    "CK_InvoiceLines_Weight_Positive",
                    "[Weight] > 0");
                table.HasCheckConstraint(
                    "CK_InvoiceLines_Quantity_Positive",
                    "[Quantity] > 0");
                table.HasCheckConstraint(
                    "CK_InvoiceLines_Price_NonNegative",
                    "[Price] >= 0");
                table.HasCheckConstraint(
                    "CK_InvoiceLines_Total_NonNegative",
                    "[Total] >= 0");
            });

        builder.HasKey(line => line.Id);

        builder.Property(line => line.Id)
            .ValueGeneratedOnAdd();

        builder.Property(line => line.CompanyId)
            .IsRequired();

        builder.HasAlternateKey(line => new
        {
            line.CompanyId,
            line.Id
        });

        builder.Property(line => line.InvoiceId)
            .IsRequired();

        builder.Property(line => line.ItemId)
            .IsRequired();

        builder.Property(line => line.ItemUnitId)
            .IsRequired();

        builder.Property(line => line.SourceInvoiceLineId);

        builder.Property(line => line.ReturnUnitCost)
            .HasPrecision(
                InventoryCostRules.UnitCostPrecision,
                InventoryCostRules.UnitCostScale);

        builder.Property(line => line.Count)
            .IsRequired();

        builder.Property(line => line.Weight)
            .HasPrecision(
                InvoiceAmountRules.QuantityPrecision,
                InvoiceAmountRules.QuantityScale)
            .IsRequired();

        builder.Property(line => line.Quantity)
            .HasPrecision(
                InvoiceAmountRules.QuantityPrecision,
                InvoiceAmountRules.QuantityScale)
            .IsRequired();

        builder.Property(line => line.Price)
            .HasPrecision(
                InvoiceAmountRules.MoneyPrecision,
                InvoiceAmountRules.MoneyScale)
            .IsRequired();

        builder.Property(line => line.Total)
            .HasPrecision(
                InvoiceAmountRules.MoneyPrecision,
                InvoiceAmountRules.MoneyScale)
            .IsRequired();

        builder.Property(line => line.BaseUnitPrice)
            .HasPrecision(
                InventoryCostRules.UnitCostPrecision,
                InventoryCostRules.UnitCostScale)
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(line => line.BaseTotal)
            .HasPrecision(
                InventoryCostRules.ValuePrecision,
                InventoryCostRules.ValueScale)
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(line => line.Notes)
            .HasMaxLength(1_000);

        builder.HasOne(line => line.Company)
            .WithMany()
            .HasForeignKey(line => line.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(line => line.Item)
            .WithMany()
            .HasForeignKey(line => new
            {
                line.CompanyId,
                line.ItemId
            })
            .HasPrincipalKey(item => new
            {
                item.CompanyId,
                item.Id
            })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(line => line.ItemUnit)
            .WithMany()
            .HasForeignKey(line => new
            {
                line.CompanyId,
                line.ItemUnitId
            })
            .HasPrincipalKey(unit => new
            {
                unit.CompanyId,
                unit.Id
            })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(line => line.SourceInvoiceLine)
            .WithMany()
            .HasForeignKey(line => new
            {
                line.CompanyId,
                line.SourceInvoiceLineId
            })
            .HasPrincipalKey(source => new
            {
                source.CompanyId,
                source.Id
            })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(line => new
        {
            line.CompanyId,
            line.SourceInvoiceLineId
        });

        builder.HasIndex(line => new
        {
            line.CompanyId,
            line.InvoiceId,
            line.ItemId
        })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
    }
}
