using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Domain.Entities.Inventory;

namespace MiniErp.Infrastructure.Persistence.Configurations;

public sealed class StockOpeningBalanceLineConfiguration
    : AuditableEntityConfiguration<StockOpeningBalanceLine>
{
    public override void Configure(EntityTypeBuilder<StockOpeningBalanceLine> builder)
    {
        base.Configure(builder);

        builder.ToTable(
            "StockOpeningBalanceLines",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_StockOpeningBalanceLines_Count_Positive",
                    "[Count] > 0");
                table.HasCheckConstraint(
                    "CK_StockOpeningBalanceLines_Weight_Positive",
                    "[Weight] > 0");
                table.HasCheckConstraint(
                    "CK_StockOpeningBalanceLines_Quantity_Positive",
                    "[Quantity] > 0");
                table.HasCheckConstraint(
                    "CK_StockOpeningBalanceLines_Price_NonNegative",
                    "[Price] >= 0");
                table.HasCheckConstraint(
                    "CK_StockOpeningBalanceLines_Total_NonNegative",
                    "[Total] >= 0");
            });
        builder.HasKey(line => line.Id);

        builder.Property(line => line.Id)
            .ValueGeneratedOnAdd();

        builder.Property(line => line.CompanyId)
            .IsRequired();

        builder.Property(line => line.StockOpeningBalanceId)
            .IsRequired();

        builder.Property(line => line.ItemId)
            .IsRequired();

        builder.Property(line => line.ItemUnitId);

        builder.Property(line => line.Count)
            .IsRequired();

        builder.Property(line => line.Weight)
            .HasPrecision(
                StockOpeningBalanceAmountRules.QuantityPrecision,
                StockOpeningBalanceAmountRules.QuantityScale)
            .IsRequired();

        builder.Property(line => line.Quantity)
            .HasPrecision(
                StockOpeningBalanceAmountRules.QuantityPrecision,
                StockOpeningBalanceAmountRules.QuantityScale)
            .IsRequired();

        builder.Property(line => line.Price)
            .HasPrecision(
                StockOpeningBalanceAmountRules.MoneyPrecision,
                StockOpeningBalanceAmountRules.MoneyScale)
            .IsRequired();

        builder.Property(line => line.Total)
            .HasPrecision(
                StockOpeningBalanceAmountRules.MoneyPrecision,
                StockOpeningBalanceAmountRules.MoneyScale)
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
            .HasPrincipalKey(itemUnit => new
            {
                itemUnit.CompanyId,
                itemUnit.Id
            })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(line => new
        {
            line.CompanyId,
            line.StockOpeningBalanceId,
            line.ItemId
        })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
    }
}
