using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Domain.Entities.Companies;

namespace MiniErp.Infrastructure.Persistence.Configurations;

public sealed class CompanyConfiguration : AuditableEntityConfiguration<Company>
{
    public override void Configure(EntityTypeBuilder<Company> builder)
    {
        base.Configure(builder);

        builder.ToTable("Companies");
        builder.HasKey(company => company.Id);

        builder.Property(company => company.Id)
            .ValueGeneratedOnAdd();

        builder.Property(company => company.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.HasIndex(company => company.Name);

        builder.Property(company => company.Address)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(company => company.CommercialRegister)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(company => company.CommercialRegister)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.Property(company => company.TaxNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(company => company.TaxNumber)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.Property(company => company.ManagerName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(company => company.RowVersion)
            .IsRowVersion()
            .IsRequired();
    }
}
