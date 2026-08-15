using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Domain.Entities.Inventory;

namespace MiniErp.Infrastructure.Persistence.Configurations;

public sealed class StockAdjustmentConfiguration
    : AuditableEntityConfiguration<StockAdjustment>
{
    public override void Configure(EntityTypeBuilder<StockAdjustment> builder)
    {
        base.Configure(builder);

        builder.ToTable("StockAdjustments");
        builder.HasKey(adjustment => adjustment.Id);

        builder.Property(adjustment => adjustment.Id)
            .ValueGeneratedOnAdd();

        builder.Property(adjustment => adjustment.CompanyId)
            .IsRequired();

        builder.HasAlternateKey(adjustment => new
        {
            adjustment.CompanyId,
            adjustment.Id
        });

        builder.Property(adjustment => adjustment.StoreId)
            .IsRequired();

        builder.Property(adjustment => adjustment.DocumentNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(adjustment => adjustment.DocumentDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(adjustment => adjustment.Direction)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(adjustment => adjustment.Reason)
            .HasMaxLength(1_000);

        builder.Property(adjustment => adjustment.LastModifiedAt)
            .HasColumnType("datetime2(7)")
            .IsRequired();

        builder.Property(adjustment => adjustment.RowVersion)
            .IsRowVersion()
            .IsRequired();

        builder.HasIndex(adjustment => new
        {
            adjustment.CompanyId,
            adjustment.DocumentNumber
        })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.HasIndex(adjustment => new
        {
            adjustment.CompanyId,
            adjustment.SourceInventoryCountId,
            adjustment.Direction
        })
            .IsUnique()
            .HasDatabaseName(
                "UX_StockAdjustments_CompanyId_SourceInventoryCountId_Direction")
            .HasFilter(
                "[SourceInventoryCountId] IS NOT NULL AND [IsDeleted] = 0");

        builder.HasOne(adjustment => adjustment.Company)
            .WithMany()
            .HasForeignKey(adjustment => adjustment.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(adjustment => adjustment.Store)
            .WithMany()
            .HasForeignKey(adjustment => new
            {
                adjustment.CompanyId,
                adjustment.StoreId
            })
            .HasPrincipalKey(store => new
            {
                store.CompanyId,
                store.Id
            })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(adjustment => adjustment.SourceInventoryCount)
            .WithMany(count => count.GeneratedStockAdjustments)
            .HasForeignKey(adjustment => new
            {
                adjustment.CompanyId,
                adjustment.SourceInventoryCountId
            })
            .HasPrincipalKey(count => new
            {
                count.CompanyId,
                count.Id
            })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(adjustment => adjustment.Lines)
            .WithOne(line => line.StockAdjustment)
            .HasForeignKey(line => new
            {
                line.CompanyId,
                line.StockAdjustmentId
            })
            .HasPrincipalKey(adjustment => new
            {
                adjustment.CompanyId,
                adjustment.Id
            })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
