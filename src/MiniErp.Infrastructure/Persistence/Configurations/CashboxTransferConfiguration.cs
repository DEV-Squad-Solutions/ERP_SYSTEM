using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Domain.Entities.CashManagement;

namespace MiniErp.Infrastructure.Persistence.Configurations;

public sealed class CashboxTransferConfiguration
    : AuditableEntityConfiguration<CashboxTransfer>
{
    public override void Configure(
        EntityTypeBuilder<CashboxTransfer> builder)
    {
        base.Configure(builder);

        builder.ToTable(
            "CashboxTransfers",
            table => table.HasCheckConstraint(
                "CK_CashboxTransfers_DifferentCashboxes",
                "[SourceCashboxId] <> [DestinationCashboxId]"));
        builder.HasKey(transfer => transfer.Id);
        builder.Property(transfer => transfer.Id).ValueGeneratedOnAdd();
        builder.Property(transfer => transfer.CompanyId).IsRequired();
        builder.HasAlternateKey(transfer => new
        {
            transfer.CompanyId,
            transfer.Id
        });

        builder.Property(transfer => transfer.TransferNumber)
            .HasMaxLength(50)
            .IsRequired();
        builder.Property(transfer => transfer.TransferDate)
            .HasColumnType("date")
            .IsRequired();
        builder.Property(transfer => transfer.SourceCashboxId)
            .IsRequired();
        builder.Property(transfer => transfer.DestinationCashboxId)
            .IsRequired();
        builder.Property(transfer => transfer.Description)
            .HasMaxLength(1_000);
        builder.Property(transfer => transfer.Notes)
            .HasMaxLength(1_000);
        builder.Property(transfer => transfer.LastModifiedAt)
            .HasColumnType("datetime2(7)")
            .IsRequired();
        builder.Property(transfer => transfer.RowVersion)
            .IsRowVersion()
            .IsRequired();

        builder.HasIndex(transfer => new
        {
            transfer.CompanyId,
            transfer.TransferNumber
        })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(transfer => new
        {
            transfer.CompanyId,
            transfer.TransferDate,
            transfer.Id
        })
            .HasFilter("[IsDeleted] = 0");

        builder.HasOne(transfer => transfer.Company)
            .WithMany()
            .HasForeignKey(transfer => transfer.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(transfer => transfer.SourceCashbox)
            .WithMany()
            .HasForeignKey(transfer => new
            {
                transfer.CompanyId,
                transfer.SourceCashboxId
            })
            .HasPrincipalKey(cashbox => new
            {
                cashbox.CompanyId,
                cashbox.Id
            })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(transfer => transfer.DestinationCashbox)
            .WithMany()
            .HasForeignKey(transfer => new
            {
                transfer.CompanyId,
                transfer.DestinationCashboxId
            })
            .HasPrincipalKey(cashbox => new
            {
                cashbox.CompanyId,
                cashbox.Id
            })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(transfer => transfer.Vouchers)
            .WithOne(voucher => voucher.CashboxTransfer)
            .HasForeignKey(voucher => new
            {
                voucher.CompanyId,
                voucher.CashboxTransferId
            })
            .HasPrincipalKey(transfer => new
            {
                transfer.CompanyId,
                transfer.Id
            })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
