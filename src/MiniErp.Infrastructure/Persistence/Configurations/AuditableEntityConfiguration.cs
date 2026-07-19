using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Domain.Common.Entities;

namespace MiniErp.Infrastructure.Persistence.Configurations;

public abstract class AuditableEntityConfiguration<TEntity>
    : IEntityTypeConfiguration<TEntity>
    where TEntity : AuditableEntity
{
    public virtual void Configure(EntityTypeBuilder<TEntity> builder)
    {
        builder.Property(entity => entity.CreatedById)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(entity => entity.CreatedByPc)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(entity => entity.UpdatedById)
            .HasMaxLength(450);

        builder.Property(entity => entity.UpdatedByPc)
            .HasMaxLength(255);

        builder.Property(entity => entity.DeletedById)
            .HasMaxLength(450);

        builder.Property(entity => entity.DeletedByPc)
            .HasMaxLength(255);

        builder.HasQueryFilter(entity => !entity.IsDeleted);
    }
}
