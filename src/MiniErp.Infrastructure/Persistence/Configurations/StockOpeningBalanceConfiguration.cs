using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Domain.Entities.Inventory;

namespace MiniErp.Infrastructure.Persistence.Configurations;

public sealed class StockOpeningBalanceConfiguration
    : AuditableEntityConfiguration<StockOpeningBalance>
{
    public override void Configure(EntityTypeBuilder<StockOpeningBalance> builder)
    {
        base.Configure(builder);

        builder.ToTable("StockOpeningBalances");
        builder.HasKey(balance => balance.Id);

        builder.Property(balance => balance.Id)
            .ValueGeneratedOnAdd();

        builder.Property(balance => balance.CompanyId)
            .IsRequired();

        builder.HasAlternateKey(balance => new
        {
            balance.CompanyId,
            balance.Id
        });

        builder.Property(balance => balance.StoreId)
            .IsRequired();

        builder.Property(balance => balance.DocumentNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(balance => balance.DocumentDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(balance => balance.Notes)
            .HasMaxLength(1_000);

        builder.Property(balance => balance.RowVersion)
            .IsRowVersion();

        builder.HasIndex(balance => new
        {
            balance.CompanyId,
            balance.DocumentNumber
        })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.HasOne(balance => balance.Company)
            .WithMany()
            .HasForeignKey(balance => balance.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(balance => balance.Store)
            .WithMany()
            .HasForeignKey(balance => new
            {
                balance.CompanyId,
                balance.StoreId
            })
            .HasPrincipalKey(store => new
            {
                store.CompanyId,
                store.Id
            })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(balance => balance.Lines)
            .WithOne(line => line.StockOpeningBalance)
            .HasForeignKey(line => new
            {
                line.CompanyId,
                line.StockOpeningBalanceId
            })
            .HasPrincipalKey(balance => new
            {
                balance.CompanyId,
                balance.Id
            })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
