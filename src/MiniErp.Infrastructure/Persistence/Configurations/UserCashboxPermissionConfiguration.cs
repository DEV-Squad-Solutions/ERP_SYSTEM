using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Infrastructure.Identity;

namespace MiniErp.Infrastructure.Persistence.Configurations;

public sealed class UserCashboxPermissionConfiguration : IEntityTypeConfiguration<UserCashboxPermission>
{
    public void Configure(EntityTypeBuilder<UserCashboxPermission> builder)
    {
        builder.ToTable("UserCashboxPermissions", table => table.HasCheckConstraint(
            "CK_UserCashboxPermissions_ExecuteIncludesView", "[CanExecute] = 0 OR [CanView] = 1"));
        builder.HasKey(permission => new { permission.UserId, permission.CompanyId, permission.CashboxId });
        builder.HasQueryFilter(permission => !permission.UserCompany.Company.IsDeleted && !permission.Cashbox.IsDeleted);
        builder.HasOne(permission => permission.UserCompany)
            .WithMany(userCompany => userCompany.CashboxPermissions)
            .HasForeignKey(permission => new { permission.UserId, permission.CompanyId })
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(permission => permission.Cashbox)
            .WithMany()
            .HasForeignKey(permission => new { permission.CompanyId, permission.CashboxId })
            .HasPrincipalKey(cashbox => new { cashbox.CompanyId, cashbox.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
