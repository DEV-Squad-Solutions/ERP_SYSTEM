using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MiniErp.Domain.Entities.BusinessPartners;
using MiniErp.Domain.Entities.CashManagement;
using MiniErp.Domain.Entities.Catalog;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Entities.Containers;
using MiniErp.Domain.Entities.Inventory;
using MiniErp.Domain.Entities.Invoicing;
using MiniErp.Domain.Entities.Logistics;
using MiniErp.Domain.Entities.ReferenceData;
using MiniErp.Infrastructure.Identity;
using MiniErp.Infrastructure.Persistence.Interceptors;
using MiniErp.Infrastructure.Persistence.Realtime;

namespace MiniErp.Infrastructure.Persistence;

public sealed class ApplicationDbContext
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    private readonly RealtimeChangeInterceptor? realtimeChangeInterceptor;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        RealtimeChangeInterceptor? realtimeChangeInterceptor = null)
        : base(options)
    {
        this.realtimeChangeInterceptor = realtimeChangeInterceptor;
    }

    public DbSet<Company> Companies => Set<Company>();

    public DbSet<CompanySettings> CompanySettings => Set<CompanySettings>();

    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();

    public DbSet<BusinessPartner> BusinessPartners => Set<BusinessPartner>();

    public DbSet<Cashbox> Cashboxes => Set<Cashbox>();

    public DbSet<CashMovementType> CashMovementTypes =>
        Set<CashMovementType>();

    public DbSet<CashVoucher> CashVouchers => Set<CashVoucher>();

    public DbSet<PartnerOpeningBalance> PartnerOpeningBalances =>
        Set<PartnerOpeningBalance>();

    public DbSet<Driver> Drivers => Set<Driver>();

    public DbSet<Item> Items => Set<Item>();

    public DbSet<ItemUnit> ItemUnits => Set<ItemUnit>();

    public DbSet<ItemsCategory> ItemsCategories => Set<ItemsCategory>();

    public DbSet<Store> Stores => Set<Store>();

    public DbSet<Country> Countries => Set<Country>();

    public DbSet<Container> Containers => Set<Container>();

    public DbSet<StoreContainer> StoreContainers => Set<StoreContainer>();

    public DbSet<StockOpeningBalance> StockOpeningBalances =>
        Set<StockOpeningBalance>();

    public DbSet<StockOpeningBalanceLine> StockOpeningBalanceLines =>
        Set<StockOpeningBalanceLine>();

    public DbSet<StockAdjustment> StockAdjustments =>
        Set<StockAdjustment>();

    public DbSet<StockAdjustmentLine> StockAdjustmentLines =>
        Set<StockAdjustmentLine>();

    public DbSet<StockTransfer> StockTransfers => Set<StockTransfer>();

    public DbSet<StockTransferLine> StockTransferLines =>
        Set<StockTransferLine>();

    public DbSet<InventoryCount> InventoryCounts =>
        Set<InventoryCount>();

    public DbSet<InventoryCountLine> InventoryCountLines =>
        Set<InventoryCountLine>();

    public DbSet<Invoice> Invoices => Set<Invoice>();

    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();

    public DbSet<InvoiceContainerLine> InvoiceContainerLines =>
        Set<InvoiceContainerLine>();

    public DbSet<InvoicePayment> InvoicePayments => Set<InvoicePayment>();

    public DbSet<ItemMovement> ItemMovements => Set<ItemMovement>();

    public DbSet<InventoryCostAllocation> InventoryCostAllocations =>
        Set<InventoryCostAllocation>();

    public DbSet<ItemStoreBalance> ItemStoreBalances =>
        Set<ItemStoreBalance>();

    public DbSet<ContainerMovement> ContainerMovements =>
        Set<ContainerMovement>();

    public DbSet<BusinessPartnerMovement> BusinessPartnerMovements =>
        Set<BusinessPartnerMovement>();

    public DbSet<DriverTrip> DriverTrips => Set<DriverTrip>();

    public DbSet<UserCompany> UserCompanies => Set<UserCompany>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<RealtimeOutboxMessage> RealtimeOutboxMessages =>
        Set<RealtimeOutboxMessage>();

    public override int SaveChanges()
    {
        realtimeChangeInterceptor?.EnqueueNotifications(this);
        return base.SaveChanges();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        realtimeChangeInterceptor?.EnqueueNotifications(this);
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        realtimeChangeInterceptor?.EnqueueNotifications(this);
        return base.SaveChangesAsync(cancellationToken);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        realtimeChangeInterceptor?.EnqueueNotifications(this);
        return base.SaveChangesAsync(
            acceptAllChangesOnSuccess,
            cancellationToken);
    }

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
