using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Domain.Entities;

namespace MiniErp.Infrastructure.Persistence.Configurations;

public sealed class ItemConfiguration : AuditableEntityConfiguration<Item>
{
    public override void Configure(EntityTypeBuilder<Item> builder)
    {
        base.Configure(builder);

        builder.ToTable("Items");
        builder.HasKey(item => item.Id);

        builder.Property(item => item.Id)
            .ValueGeneratedOnAdd();

        builder.Property(item => item.Code)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(item => new { item.CompanyId, item.Code })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.Property(item => item.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(item => item.Description)
            .HasMaxLength(1_000);

        builder.HasOne(item => item.Company)
            .WithMany()
            .HasForeignKey(item => item.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(item => item.ItemUnit)
            .WithMany(itemUnit => itemUnit.Items)
            .HasForeignKey(item => new { item.CompanyId, item.ItemUnitId })
            .HasPrincipalKey(itemUnit => new { itemUnit.CompanyId, itemUnit.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
