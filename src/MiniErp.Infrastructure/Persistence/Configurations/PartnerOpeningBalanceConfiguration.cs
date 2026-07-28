using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Domain.Entities.BusinessPartners;

namespace MiniErp.Infrastructure.Persistence.Configurations;

public sealed class PartnerOpeningBalanceConfiguration
    : AuditableEntityConfiguration<PartnerOpeningBalance>
{
    public override void Configure(EntityTypeBuilder<PartnerOpeningBalance> builder)
    {
        base.Configure(builder);

        builder.ToTable("PartnerOpeningBalances");
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

        builder.Property(balance => balance.BusinessPartnerId)
            .IsRequired();

        builder.Property(balance => balance.DocumentNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(balance => balance.DocumentDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(balance => balance.Currency)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(balance => balance.BalanceType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(balance => balance.Amount)
            .HasPrecision(
                PartnerOpeningBalanceAmountRules.MoneyPrecision,
                PartnerOpeningBalanceAmountRules.MoneyScale)
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

        builder.HasOne(balance => balance.BusinessPartner)
            .WithMany()
            .HasForeignKey(balance => new
            {
                balance.CompanyId,
                balance.BusinessPartnerId
            })
            .HasPrincipalKey(partner => new
            {
                partner.CompanyId,
                partner.Id
            })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
