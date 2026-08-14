using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MiniErp.Infrastructure.Persistence.Configurations;

public sealed class EntityIdentifierSequenceConfiguration
    : IEntityTypeConfiguration<EntityIdentifierSequence>
{
    public void Configure(
        EntityTypeBuilder<EntityIdentifierSequence> builder)
    {
        builder.ToTable("EntityIdentifierSequences");

        builder.HasKey(sequence => new
        {
            sequence.Scope,
            sequence.Prefix
        });

        builder.Property(sequence => sequence.Scope)
            .HasMaxLength(32);

        builder.Property(sequence => sequence.Prefix)
            .HasMaxLength(16);

        builder.Property(sequence => sequence.LastNumber)
            .IsRequired();
    }
}
