using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Domain.Entities.Invoicing;

namespace MiniErp.Infrastructure.Persistence.Configurations;

public sealed class InvoiceContainerLineConfiguration
    : AuditableEntityConfiguration<InvoiceContainerLine>
{
    public override void Configure(
        EntityTypeBuilder<InvoiceContainerLine> builder)
    {
        base.Configure(builder);

        builder.ToTable(
            "InvoiceContainerLines",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_InvoiceContainerLines_Units_NonNegative",
                    "[OutgoingUnits] >= 0 AND [IncomingUnits] >= 0");
                table.HasCheckConstraint(
                    "CK_InvoiceContainerLines_Units_NotBothZero",
                    "[OutgoingUnits] > 0 OR [IncomingUnits] > 0");
            });

        builder.HasKey(line => line.Id);

        builder.Property(line => line.Id)
            .ValueGeneratedOnAdd();

        builder.Property(line => line.CompanyId)
            .IsRequired();

        builder.HasAlternateKey(line => new
        {
            line.CompanyId,
            line.Id
        });

        builder.Property(line => line.InvoiceId)
            .IsRequired();

        builder.Property(line => line.ContainerId)
            .IsRequired();

        builder.Property(line => line.OutgoingUnits)
            .IsRequired();

        builder.Property(line => line.IncomingUnits)
            .IsRequired();

        builder.HasOne(line => line.Company)
            .WithMany()
            .HasForeignKey(line => line.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(line => line.Container)
            .WithMany()
            .HasForeignKey(line => new
            {
                line.CompanyId,
                line.ContainerId
            })
            .HasPrincipalKey(container => new
            {
                container.CompanyId,
                container.Id
            })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(line => new
        {
            line.CompanyId,
            line.InvoiceId,
            line.ContainerId
        })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
    }
}
