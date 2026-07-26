using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MiniErp.Domain.Entities.BusinessPartners;
using MiniErp.Domain.Entities.Catalog;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Entities.Containers;
using MiniErp.Domain.Entities.Inventory;
using MiniErp.Domain.Entities.Invoicing;
using MiniErp.Domain.Entities.Logistics;
using MiniErp.Domain.Entities.ReferenceData;
using MiniErp.Infrastructure.Identity;

namespace MiniErp.Infrastructure.Persistence;

public sealed class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Company> Companies => Set<Company>();

    public DbSet<BusinessPartner> BusinessPartners => Set<BusinessPartner>();

    public DbSet<PartnerOpeningBalance> PartnerOpeningBalances =>
        Set<PartnerOpeningBalance>();

    public DbSet<Driver> Drivers => Set<Driver>();

    public DbSet<Item> Items => Set<Item>();

    public DbSet<ItemUnit> ItemUnits => Set<ItemUnit>();

    public DbSet<Store> Stores => Set<Store>();

    public DbSet<Country> Countries => Set<Country>();

    public DbSet<Container> Containers => Set<Container>();

    public DbSet<StoreContainer> StoreContainers => Set<StoreContainer>();

    public DbSet<StockOpeningBalance> StockOpeningBalances =>
        Set<StockOpeningBalance>();

    public DbSet<StockOpeningBalanceLine> StockOpeningBalanceLines =>
        Set<StockOpeningBalanceLine>();

    public DbSet<Invoice> Invoices => Set<Invoice>();

    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();

    public DbSet<InvoiceContainerLine> InvoiceContainerLines =>
        Set<InvoiceContainerLine>();

    public DbSet<ItemMovement> ItemMovements => Set<ItemMovement>();

    public DbSet<ContainerMovement> ContainerMovements =>
        Set<ContainerMovement>();

    public DbSet<BusinessPartnerMovement> BusinessPartnerMovements =>
        Set<BusinessPartnerMovement>();

    public DbSet<DriverTrip> DriverTrips => Set<DriverTrip>();

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
