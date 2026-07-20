using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Infrastructure.Identity;

namespace MiniErp.Infrastructure.Persistence.Configurations;

public sealed class UserCompanyConfiguration : IEntityTypeConfiguration<UserCompany>
{
    public void Configure(EntityTypeBuilder<UserCompany> builder)
    {
        builder.ToTable("UserCompanies");
        builder.HasKey(userCompany => new
        {
            userCompany.UserId,
            userCompany.CompanyId
        });

        builder.HasQueryFilter(userCompany => !userCompany.Company.IsDeleted);

        builder.HasOne(userCompany => userCompany.User)
            .WithMany(user => user.UserCompanies)
            .HasForeignKey(userCompany => userCompany.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(userCompany => userCompany.Company)
            .WithMany()
            .HasForeignKey(userCompany => userCompany.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
