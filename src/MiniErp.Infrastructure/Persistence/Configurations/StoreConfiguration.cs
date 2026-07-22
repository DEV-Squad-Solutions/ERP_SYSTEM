using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Domain.Entities.Inventory;

namespace MiniErp.Infrastructure.Persistence.Configurations;

public sealed class StoreConfiguration : AuditableEntityConfiguration<Store>
{
    public override void Configure(EntityTypeBuilder<Store> builder)
    {
        base.Configure(builder);

        builder.ToTable(
            "Stores",
            table => table.HasCheckConstraint(
                "CK_Stores_TypeBusinessPartner",
                "([IsContainerStore] = 0 AND [BusinessPartnerId] IS NULL) OR " +
                "([IsContainerStore] = 1 AND [BusinessPartnerId] IS NOT NULL)"));
        builder.HasKey(store => store.Id);

        // Container persistence will be added with its own feature. Ignoring the
        // navigation prevents convention-based table discovery in the meantime.
        builder.Ignore(store => store.StoreContainers);

        builder.Property(store => store.Id)
            .ValueGeneratedOnAdd();

        builder.Property(store => store.Code)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(store => new { store.CompanyId, store.Code })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.Property(store => store.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.HasIndex(store => store.Name);

        builder.Property(store => store.Address)
            .HasMaxLength(500);

        builder.Property(store => store.IsContainerStore)
            .HasDefaultValue(false)
            .IsRequired();

        builder.HasOne(store => store.Company)
            .WithMany()
            .HasForeignKey(store => store.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(store => store.BusinessPartner)
            .WithMany()
            .HasForeignKey(store => new
            {
                store.CompanyId,
                store.BusinessPartnerId
            })
            .HasPrincipalKey(partner => new
            {
                partner.CompanyId,
                partner.Id
            })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(store => new
        {
            store.CompanyId,
            store.BusinessPartnerId
        })
            .IsUnique()
            .HasDatabaseName(
                "UX_Stores_CompanyId_BusinessPartnerId_ActiveContainer")
            .HasFilter(
                "[BusinessPartnerId] IS NOT NULL AND " +
                "[IsContainerStore] = 1 AND " +
                "[IsActive] = 1 AND " +
                "[IsDeleted] = 0");
    }
}
