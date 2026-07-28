using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Domain.Entities.Containers;

namespace MiniErp.Infrastructure.Persistence.Configurations;

public sealed class StoreContainerConfiguration
    : AuditableEntityConfiguration<StoreContainer>
{
    public override void Configure(EntityTypeBuilder<StoreContainer> builder)
    {
        base.Configure(builder);

        builder.ToTable("StoreContainers");
        builder.HasKey(assignment => assignment.Id);

        builder.Property(assignment => assignment.Id)
            .ValueGeneratedOnAdd();

        builder.Property(assignment => assignment.CompanyId)
            .IsRequired();

        builder.Property(assignment => assignment.StoreId)
            .IsRequired();

        builder.Property(assignment => assignment.ContainerId)
            .IsRequired();


        builder.HasOne(assignment => assignment.Company)
            .WithMany()
            .HasForeignKey(assignment => assignment.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(assignment => assignment.Store)
            .WithMany(store => store.StoreContainers)
            .HasForeignKey(assignment => new
            {
                assignment.CompanyId,
                assignment.StoreId
            })
            .HasPrincipalKey(store => new
            {
                store.CompanyId,
                store.Id
            })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(assignment => assignment.Container)
            .WithMany(container => container.StoreContainers)
            .HasForeignKey(assignment => new
            {
                assignment.CompanyId,
                assignment.ContainerId
            })
            .HasPrincipalKey(container => new
            {
                container.CompanyId,
                container.Id
            })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(assignment => new
        {
            assignment.CompanyId,
            assignment.StoreId
        });

        builder.HasIndex(assignment => new
        {
            assignment.CompanyId,
            assignment.ContainerId
        });

        builder.HasIndex(assignment => new
        {
            assignment.CompanyId,
            assignment.StoreId,
            assignment.ContainerId
        })
            .IsUnique()
            .HasDatabaseName(
                "UX_StoreContainers_CompanyId_StoreId_ContainerId_Active")
            .HasFilter("[IsActive] = 1 AND [IsDeleted] = 0");
    }
}
