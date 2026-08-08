using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Infrastructure.Persistence.Realtime;

namespace MiniErp.Infrastructure.Persistence.Configurations;

public sealed class RealtimeOutboxMessageConfiguration
    : IEntityTypeConfiguration<RealtimeOutboxMessage>
{
    public void Configure(EntityTypeBuilder<RealtimeOutboxMessage> builder)
    {
        builder.ToTable("RealtimeOutboxMessages");

        builder.HasKey(message => message.Id);

        builder.Property(message => message.CompanyId)
            .IsRequired();

        builder.Property(message => message.OccurredAtUtc)
            .IsRequired();

        builder.Property(message => message.Payload)
            .IsRequired();

        builder.Property(message => message.LastError)
            .HasMaxLength(2_000);

        builder.HasIndex(message => new
            {
                message.DispatchedAtUtc,
                message.NextAttemptAtUtc,
                message.OccurredAtUtc
            })
            .HasDatabaseName("IX_RealtimeOutboxMessages_Dispatch");

        builder.HasIndex(message => new
            {
                message.CompanyId,
                message.OccurredAtUtc
            });
    }
}
