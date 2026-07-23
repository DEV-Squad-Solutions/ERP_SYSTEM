using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Domain.Entities.Containers;

namespace MiniErp.Infrastructure.Persistence.Configurations;

public sealed class ContainerConfiguration
    : AuditableEntityConfiguration<Container>
{
    public override void Configure(EntityTypeBuilder<Container> builder)
    {
        base.Configure(builder);

        builder.ToTable("Containers");
        builder.HasKey(container => container.Id);

        builder.Property(container => container.Id)
            .ValueGeneratedOnAdd();

        builder.HasAlternateKey(container => new
        {
            container.CompanyId,
            container.Id
        });

        builder.Property(container => container.Code)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(container => new
        {
            container.CompanyId,
            container.Code
        })
            .IsUnique()
            .HasDatabaseName("UX_Containers_CompanyId_Code_Active")
            .HasFilter("[IsActive] = 1 AND [IsDeleted] = 0");

        builder.Property(container => container.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.HasIndex(container => new
        {
            container.CompanyId,
            container.Name
        });

        builder.Property(container => container.Description)
            .HasMaxLength(1_000);

        builder.HasOne(container => container.Company)
            .WithMany()
            .HasForeignKey(container => container.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
