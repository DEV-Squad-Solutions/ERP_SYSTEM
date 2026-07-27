using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Domain.Entities.CashManagement;

namespace MiniErp.Infrastructure.Persistence.Configurations;

public sealed class CashMovementTypeConfiguration
    : AuditableEntityConfiguration<CashMovementType>
{
    public override void Configure(EntityTypeBuilder<CashMovementType> builder)
    {
        base.Configure(builder);

        builder.ToTable(
            "CashMovementTypes",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_CashMovementTypes_Direction",
                    "[Direction] IN (1, 2)");
                table.HasCheckConstraint(
                    "CK_CashMovementTypes_PartnerEffect",
                    "[PartnerEffect] IN (0, 1, 2)");
            });

        builder.HasKey(movementType => movementType.Id);

        builder.Property(movementType => movementType.Id)
            .ValueGeneratedOnAdd();

        builder.Property(movementType => movementType.CompanyId)
            .IsRequired();

        builder.HasAlternateKey(movementType => new
        {
            movementType.CompanyId,
            movementType.Id
        });

        builder.Property(movementType => movementType.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(movementType => movementType.Direction)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(movementType => movementType.PartnerEffect)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(movementType => movementType.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(movementType => movementType.Notes)
            .HasMaxLength(1_000);

        builder.Property(movementType => movementType.RowVersion)
            .IsRowVersion()
            .IsRequired();

        builder.HasIndex(movementType => new
        {
            movementType.CompanyId,
            movementType.Direction,
            movementType.Name
        })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.HasIndex(movementType => new
        {
            movementType.CompanyId,
            movementType.Direction,
            movementType.IsActive,
            movementType.Name,
            movementType.Id
        });

        builder.HasOne(movementType => movementType.Company)
            .WithMany()
            .HasForeignKey(movementType => movementType.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
