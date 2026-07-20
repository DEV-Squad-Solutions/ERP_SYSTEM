using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MiniErp.Domain.Entities;
using MiniErp.Infrastructure.Identity;

namespace MiniErp.Infrastructure.Persistence;

public sealed class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Company> Companies => Set<Company>();

    public DbSet<Item> Items => Set<Item>();

    public DbSet<ItemUnit> ItemUnits => Set<ItemUnit>();

    public DbSet<Store> Stores => Set<Store>();

    public DbSet<UserCompany> UserCompanies => Set<UserCompany>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(user => user.FirstName)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(user => user.LastName)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(user => user.ProfileImage)
                .IsRequired();
        });
    }
}
