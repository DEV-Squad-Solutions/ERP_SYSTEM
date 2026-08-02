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
                table.HasCheckConstraint(
                    "CK_CashMovementTypes_InvoiceDefaults",
                    "(([IsDefaultForSales] = 0 AND " +
                    "[IsDefaultForPurchaseReturn] = 0) OR " +
                    "([IsActive] = 1 AND [Direction] = 1 AND " +
                    "[PartnerEffect] = 2)) AND " +
                    "(([IsDefaultForPurchase] = 0 AND " +
                    "[IsDefaultForSalesReturn] = 0) OR " +
                    "([IsActive] = 1 AND [Direction] = 2 AND " +
                    "[PartnerEffect] = 1))");
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

        builder.Property(movementType => movementType.IsDefaultForSales)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(movementType => movementType.IsDefaultForPurchase)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(movementType => movementType.IsDefaultForSalesReturn)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(movementType => movementType.IsDefaultForPurchaseReturn)
            .HasDefaultValue(false)
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

        builder.HasIndex(movementType => new
        {
            movementType.CompanyId,
            movementType.IsDefaultForSales
        })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0 AND [IsDefaultForSales] = 1")
            .HasDatabaseName(
                "IX_CashMovementTypes_CompanyId_DefaultForSales");

        builder.HasIndex(movementType => new
        {
            movementType.CompanyId,
            movementType.IsDefaultForPurchase
        })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0 AND [IsDefaultForPurchase] = 1")
            .HasDatabaseName(
                "IX_CashMovementTypes_CompanyId_DefaultForPurchase");

        builder.HasIndex(movementType => new
        {
            movementType.CompanyId,
            movementType.IsDefaultForSalesReturn
        })
            .IsUnique()
            .HasFilter(
                "[IsDeleted] = 0 AND [IsDefaultForSalesReturn] = 1")
            .HasDatabaseName(
                "IX_CashMovementTypes_CompanyId_DefaultForSalesReturn");

        builder.HasIndex(movementType => new
        {
            movementType.CompanyId,
            movementType.IsDefaultForPurchaseReturn
        })
            .IsUnique()
            .HasFilter(
                "[IsDeleted] = 0 AND [IsDefaultForPurchaseReturn] = 1")
            .HasDatabaseName(
                "IX_CashMovementTypes_CompanyId_DefaultForPurchaseReturn");

        builder.HasOne(movementType => movementType.Company)
            .WithMany()
            .HasForeignKey(movementType => movementType.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
