using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Domain.Entities.Catalog;

namespace MiniErp.Infrastructure.Persistence.Configurations;

public sealed class ItemUnitConfiguration : AuditableEntityConfiguration<ItemUnit>
{
    public override void Configure(EntityTypeBuilder<ItemUnit> builder)
    {
        base.Configure(builder);

        builder.ToTable("ItemUnits");
        builder.HasKey(itemUnit => itemUnit.Id);

        builder.Property(itemUnit => itemUnit.Id)
            .ValueGeneratedOnAdd();

        builder.Property(itemUnit => itemUnit.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasAlternateKey(itemUnit => new { itemUnit.CompanyId, itemUnit.Id });

        builder.HasIndex(itemUnit => new { itemUnit.CompanyId, itemUnit.Name })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.HasOne(itemUnit => itemUnit.Company)
            .WithMany()
            .HasForeignKey(itemUnit => itemUnit.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
