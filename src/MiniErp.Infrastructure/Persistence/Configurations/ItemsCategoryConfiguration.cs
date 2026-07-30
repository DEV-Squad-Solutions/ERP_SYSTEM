using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Domain.Entities.Catalog;

namespace MiniErp.Infrastructure.Persistence.Configurations;

public sealed class ItemsCategoryConfiguration
    : AuditableEntityConfiguration<ItemsCategory>
{
    public override void Configure(EntityTypeBuilder<ItemsCategory> builder)
    {
        base.Configure(builder);

        builder.ToTable("ItemsCategories");
        builder.HasKey(category => category.Id);

        builder.Property(category => category.Id)
            .ValueGeneratedOnAdd();

        builder.Property(category => category.CompanyId)
            .IsRequired();

        builder.HasAlternateKey(category => new
        {
            category.CompanyId,
            category.Id
        });

        builder.Property(category => category.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(category => category.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(category => category.Notes)
            .HasMaxLength(1_000);

        builder.Property(category => category.RowVersion)
            .IsRowVersion()
            .IsRequired();

        builder.HasIndex(category => new
        {
            category.CompanyId,
            category.Name
        })
            .IsUnique()
            .HasFilter("[IsActive] = 1 AND [IsDeleted] = 0");

        builder.HasIndex(category => new
        {
            category.CompanyId,
            category.IsActive,
            category.Name,
            category.Id
        });

        builder.HasOne(category => category.Company)
            .WithMany()
            .HasForeignKey(category => category.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
