using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Domain.Entities.Inventory;

namespace MiniErp.Infrastructure.Persistence.Configurations;

public sealed class InventoryCountConfiguration
    : AuditableEntityConfiguration<InventoryCount>
{
    public override void Configure(EntityTypeBuilder<InventoryCount> builder)
    {
        base.Configure(builder);

        builder.ToTable("InventoryCounts");
        builder.HasKey(count => count.Id);

        builder.Property(count => count.Id)
            .ValueGeneratedOnAdd();

        builder.Property(count => count.CompanyId)
            .IsRequired();

        builder.HasAlternateKey(count => new
        {
            count.CompanyId,
            count.Id
        });

        builder.Property(count => count.StoreId)
            .IsRequired();

        builder.Property(count => count.DocumentNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(count => count.CountDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(count => count.SnapshotTakenAt)
            .HasColumnType("datetime2(7)")
            .IsRequired();

        builder.Property(count => count.ReconciledAt)
            .HasColumnType("datetime2(7)");

        builder.Property(count => count.Notes)
            .HasMaxLength(1_000);

        builder.Property(count => count.LastModifiedAt)
            .HasColumnType("datetime2(7)")
            .IsRequired();

        builder.Property(count => count.RowVersion)
            .IsRowVersion()
            .IsRequired();

        builder.HasIndex(count => new
        {
            count.CompanyId,
            count.DocumentNumber
        })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.HasOne(count => count.Company)
            .WithMany()
            .HasForeignKey(count => count.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(count => count.Store)
            .WithMany()
            .HasForeignKey(count => new
            {
                count.CompanyId,
                count.StoreId
            })
            .HasPrincipalKey(store => new
            {
                store.CompanyId,
                store.Id
            })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(count => count.Lines)
            .WithOne(line => line.InventoryCount)
            .HasForeignKey(line => new
            {
                line.CompanyId,
                line.InventoryCountId
            })
            .HasPrincipalKey(count => new
            {
                count.CompanyId,
                count.Id
            })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
