using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Domain.Entities;

namespace MiniErp.Infrastructure.Persistence.Configurations;

public sealed class BusinessPartnerConfiguration
    : AuditableEntityConfiguration<BusinessPartner>
{
    public override void Configure(EntityTypeBuilder<BusinessPartner> builder)
    {
        base.Configure(builder);

        builder.ToTable("BusinessPartners");
        builder.HasKey(partner => partner.Id);

        builder.Property(partner => partner.Id)
            .ValueGeneratedOnAdd();

        builder.Property(partner => partner.Code)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(partner => new { partner.CompanyId, partner.Code })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.Property(partner => partner.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.HasIndex(partner => new { partner.CompanyId, partner.Name });

        builder.Property(partner => partner.PhoneNumber)
            .HasMaxLength(50);

        builder.Property(partner => partner.Email)
            .HasMaxLength(256);

        builder.Property(partner => partner.Address)
            .HasMaxLength(500);

        builder.Property(partner => partner.TaxNumber)
            .HasMaxLength(100);

        builder.HasIndex(partner => new { partner.CompanyId, partner.TaxNumber })
            .IsUnique()
            .HasFilter("[TaxNumber] IS NOT NULL AND [IsDeleted] = 0");

        builder.Property(partner => partner.Currency)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(partner => partner.CreditLimit)
            .HasPrecision(18, 2);

        builder.HasOne(partner => partner.Company)
            .WithMany()
            .HasForeignKey(partner => partner.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
