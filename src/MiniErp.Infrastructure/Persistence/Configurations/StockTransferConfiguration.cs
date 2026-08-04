using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Domain.Entities.Inventory;

namespace MiniErp.Infrastructure.Persistence.Configurations;

public sealed class StockTransferConfiguration
    : AuditableEntityConfiguration<StockTransfer>
{
    public override void Configure(EntityTypeBuilder<StockTransfer> builder)
    {
        base.Configure(builder);

        builder.ToTable("StockTransfers");
        builder.HasKey(transfer => transfer.Id);
        builder.Property(transfer => transfer.Id).ValueGeneratedOnAdd();
        builder.Property(transfer => transfer.CompanyId).IsRequired();
        builder.HasAlternateKey(transfer => new
        {
            transfer.CompanyId,
            transfer.Id
        });

        builder.Property(transfer => transfer.DocumentNumber)
            .HasMaxLength(50)
            .IsRequired();
        builder.Property(transfer => transfer.TransferDate)
            .HasColumnType("date")
            .IsRequired();
        builder.Property(transfer => transfer.SourceStoreId).IsRequired();
        builder.Property(transfer => transfer.DestinationStoreId).IsRequired();
        builder.Property(transfer => transfer.Notes).HasMaxLength(1_000);
        builder.Property(transfer => transfer.LastModifiedAt)
            .HasColumnType("datetime2(7)")
            .IsRequired();
        builder.Property(transfer => transfer.RowVersion)
            .IsRowVersion()
            .IsRequired();

        builder.HasIndex(transfer => new
        {
            transfer.CompanyId,
            transfer.DocumentNumber
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
        builder.HasOne(transfer => transfer.SourceStore)
            .WithMany()
            .HasForeignKey(transfer => new
            {
                transfer.CompanyId,
                transfer.SourceStoreId
            })
            .HasPrincipalKey(store => new
            {
                store.CompanyId,
                store.Id
            })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(transfer => transfer.DestinationStore)
            .WithMany()
            .HasForeignKey(transfer => new
            {
                transfer.CompanyId,
                transfer.DestinationStoreId
            })
            .HasPrincipalKey(store => new
            {
                store.CompanyId,
                store.Id
            })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(transfer => transfer.Lines)
            .WithOne(line => line.StockTransfer)
            .HasForeignKey(line => new
            {
                line.CompanyId,
                line.StockTransferId
            })
            .HasPrincipalKey(transfer => new
            {
                transfer.CompanyId,
                transfer.Id
            })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
