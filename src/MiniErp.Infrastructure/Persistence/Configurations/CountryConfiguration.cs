using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Domain.Entities.ReferenceData;

namespace MiniErp.Infrastructure.Persistence.Configurations;

public sealed class CountryConfiguration
    : AuditableEntityConfiguration<Country>
{
    public override void Configure(EntityTypeBuilder<Country> builder)
    {
        base.Configure(builder);

        builder.ToTable("Countries");
        builder.HasKey(country => country.Id);

        builder.Property(country => country.Id)
            .ValueGeneratedOnAdd();

        builder.Property(country => country.Code)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(country => country.Code)
            .IsUnique()
            .HasDatabaseName("UX_Countries_Code_Active")
            .HasFilter("[IsActive] = 1 AND [IsDeleted] = 0");

        builder.Property(country => country.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.HasIndex(country => country.Name);

        builder.Property(country => country.ArabicName)
            .HasMaxLength(200)
            .IsRequired();

    }
}
