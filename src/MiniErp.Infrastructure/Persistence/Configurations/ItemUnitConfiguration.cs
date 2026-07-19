using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Domain.Entities;

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

        builder.HasIndex(itemUnit => itemUnit.Name)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
    }
}
