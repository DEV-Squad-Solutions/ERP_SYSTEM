using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Domain.Entities;

namespace MiniErp.Infrastructure.Persistence.Configurations;

public sealed class StoreConfiguration : AuditableEntityConfiguration<Store>
{
    public override void Configure(EntityTypeBuilder<Store> builder)
    {
        base.Configure(builder);

        builder.ToTable("Stores");
        builder.HasKey(store => store.Id);

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

        builder.HasOne(store => store.Company)
            .WithMany()
            .HasForeignKey(store => store.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
