using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Domain.Entities.Inventory;

namespace MiniErp.Infrastructure.Persistence.Configurations;

public sealed class StockTransferLineConfiguration
    : AuditableEntityConfiguration<StockTransferLine>
{
    public override void Configure(EntityTypeBuilder<StockTransferLine> builder)
    {
        base.Configure(builder);

        builder.ToTable(
            "StockTransferLines",
            table => table.HasCheckConstraint(
                "CK_StockTransferLines_Quantity_Positive",
                "[Quantity] > 0"));
        builder.HasKey(line => line.Id);
        builder.Property(line => line.Id).ValueGeneratedOnAdd();
        builder.Property(line => line.CompanyId).IsRequired();
        builder.Property(line => line.StockTransferId).IsRequired();
        builder.Property(line => line.ItemId).IsRequired();
        builder.Property(line => line.ItemUnitId).IsRequired();
        builder.Property(line => line.Quantity)
            .HasPrecision(
                InventoryQuantityRules.Precision,
                InventoryQuantityRules.Scale)
            .IsRequired();
        builder.Property(line => line.Notes).HasMaxLength(1_000);

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
        builder.HasIndex(line => new
        {
            line.CompanyId,
            line.StockTransferId,
            line.ItemId
        })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
    }
}
