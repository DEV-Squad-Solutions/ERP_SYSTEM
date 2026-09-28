using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using MiniErp.Domain.Entities.Accounting;
using MiniErp.Domain.Entities.BusinessPartners;
using MiniErp.Domain.Entities.CashManagement;
using MiniErp.Domain.Entities.Containers;
using MiniErp.Domain.Entities.Employees;
using MiniErp.Domain.Entities.Inventory;
using MiniErp.Domain.Entities.Invoicing;
using MiniErp.Domain.Entities.Logistics;
using MiniErp.Domain.Entities.Payroll;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Enums;

namespace MiniErp.Infrastructure.Persistence;

public sealed partial class ApplicationDbContext
{
    private static readonly Type[] FiscalYearTransactionTypes =
    [
        typeof(Invoice),
        typeof(InvoicePayment),
        typeof(CashVoucher),
        typeof(CashboxTransfer),
        typeof(CashboxRevaluation),
        typeof(MonetaryAccountRevaluation),
        typeof(PartnerOpeningBalance),
        typeof(BusinessPartnerMovement),
        typeof(EmployeeOpeningBalance),
        typeof(EmployeeMovement),
        typeof(EmployeeAttendance),
        typeof(PayrollEntry),
        typeof(DriverTrip),
        typeof(StockOpeningBalance),
        typeof(StockAdjustment),
        typeof(StockTransfer),
        typeof(InventoryCount),
        typeof(ItemMovement),
        typeof(ContainerMovement),
        typeof(ExchangeRate)
    ];

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        AssignTransactionFiscalYears();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default) =>
        SaveChangesWithFiscalYearsAsync(
            acceptAllChangesOnSuccess,
            cancellationToken);

    private async Task<int> SaveChangesWithFiscalYearsAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken)
    {
        await AssignTransactionFiscalYearsAsync(cancellationToken);
        return await base.SaveChangesAsync(
            acceptAllChangesOnSuccess,
            cancellationToken);
    }

    private static void ConfigureTransactionFiscalYears(
        ModelBuilder builder)
    {
        foreach (var transactionType in FiscalYearTransactionTypes)
        {
            var entity = builder.Entity(transactionType);
            entity.Property<int>(nameof(Invoice.FiscalYearId))
                .IsRequired();
            entity.HasIndex(
                    nameof(Invoice.CompanyId),
                    nameof(Invoice.FiscalYearId))
                .HasDatabaseName(
                    $"IX_{entity.Metadata.GetTableName()}_Company_FiscalYear");
            entity.HasOne(typeof(FiscalYear), nameof(Invoice.FiscalYear))
                .WithMany()
                .HasForeignKey(
                    nameof(Invoice.CompanyId),
                    nameof(Invoice.FiscalYearId))
                .HasPrincipalKey(
                    nameof(FiscalYear.CompanyId),
                    nameof(FiscalYear.Id))
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    private void AssignTransactionFiscalYears()
    {
        foreach (var scope in FiscalYearScopesToAssign())
        {
            var fiscalYear = FiscalYears
                .AsNoTracking()
                .SingleOrDefault(year =>
                    year.CompanyId == scope.CompanyId &&
                    year.StartDate <= scope.Date &&
                    year.EndDate >= scope.Date);
            ApplyFiscalYear(scope, fiscalYear);
        }

        AssignInvoicePaymentFiscalYears();
    }

    private async Task AssignTransactionFiscalYearsAsync(
        CancellationToken cancellationToken)
    {
        foreach (var scope in FiscalYearScopesToAssign())
        {
            var fiscalYear = await FiscalYears
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    year =>
                        year.CompanyId == scope.CompanyId &&
                        year.StartDate <= scope.Date &&
                        year.EndDate >= scope.Date,
                    cancellationToken);
            ApplyFiscalYear(scope, fiscalYear);
        }

        await AssignInvoicePaymentFiscalYearsAsync(cancellationToken);
    }

    private IReadOnlyList<TransactionFiscalYearScope>
        FiscalYearScopesToAssign() =>
        ChangeTracker.Entries()
            .Where(entry =>
                FiscalYearTransactionTypes.Contains(entry.Metadata.ClrType) &&
                entry.Entity is not InvoicePayment &&
                (entry.State == EntityState.Added ||
                 entry.State == EntityState.Modified))
            .Select(CreateFiscalYearScope)
            .Where(scope => scope is not null)
            .Cast<TransactionFiscalYearScope>()
            .ToArray();

    private static TransactionFiscalYearScope? CreateFiscalYearScope(
        EntityEntry entry)
    {
        var (companyId, fiscalYearId, date, dateProperty) = entry.Entity switch
        {
            Invoice row =>
                (row.CompanyId, row.FiscalYearId, row.InvoiceDate,
                    nameof(Invoice.InvoiceDate)),
            CashVoucher row =>
                (row.CompanyId, row.FiscalYearId, row.VoucherDate,
                    nameof(CashVoucher.VoucherDate)),
            CashboxTransfer row =>
                (row.CompanyId, row.FiscalYearId, row.TransferDate,
                    nameof(CashboxTransfer.TransferDate)),
            CashboxRevaluation row =>
                (row.CompanyId, row.FiscalYearId, row.RevaluationDate,
                    nameof(CashboxRevaluation.RevaluationDate)),
            MonetaryAccountRevaluation row =>
                (row.CompanyId, row.FiscalYearId, row.RevaluationDate,
                    nameof(MonetaryAccountRevaluation.RevaluationDate)),
            PartnerOpeningBalance row =>
                (row.CompanyId, row.FiscalYearId, row.DocumentDate,
                    nameof(PartnerOpeningBalance.DocumentDate)),
            BusinessPartnerMovement row =>
                (row.CompanyId, row.FiscalYearId, row.MovementDate,
                    nameof(BusinessPartnerMovement.MovementDate)),
            EmployeeOpeningBalance row =>
                (row.CompanyId, row.FiscalYearId, row.DocumentDate,
                    nameof(EmployeeOpeningBalance.DocumentDate)),
            EmployeeMovement row =>
                (row.CompanyId, row.FiscalYearId, row.MovementDate,
                    nameof(EmployeeMovement.MovementDate)),
            EmployeeAttendance row =>
                (row.CompanyId, row.FiscalYearId, row.WorkDate,
                    nameof(EmployeeAttendance.WorkDate)),
            PayrollEntry row =>
                (row.CompanyId, row.FiscalYearId, row.StartDate,
                    nameof(PayrollEntry.StartDate)),
            DriverTrip row =>
                (row.CompanyId, row.FiscalYearId, row.TripDate,
                    nameof(DriverTrip.TripDate)),
            StockOpeningBalance row =>
                (row.CompanyId, row.FiscalYearId, row.DocumentDate,
                    nameof(StockOpeningBalance.DocumentDate)),
            StockAdjustment row =>
                (row.CompanyId, row.FiscalYearId, row.DocumentDate,
                    nameof(StockAdjustment.DocumentDate)),
            StockTransfer row =>
                (row.CompanyId, row.FiscalYearId, row.TransferDate,
                    nameof(StockTransfer.TransferDate)),
            InventoryCount row =>
                (row.CompanyId, row.FiscalYearId, row.CountDate,
                    nameof(InventoryCount.CountDate)),
            ItemMovement row =>
                (row.CompanyId, row.FiscalYearId, row.MovementDate,
                    nameof(ItemMovement.MovementDate)),
            ContainerMovement row =>
                (row.CompanyId, row.FiscalYearId, row.MovementDate,
                    nameof(ContainerMovement.MovementDate)),
            ExchangeRate row =>
                (row.CompanyId, row.FiscalYearId, row.RateDate,
                    nameof(ExchangeRate.RateDate)),
            _ => default
        };

        if (companyId <= 0 || date == default)
        {
            return null;
        }

        var dateChanged = entry.State == EntityState.Modified &&
            entry.Property(dateProperty).IsModified;
        return fiscalYearId <= 0 || dateChanged
            ? new TransactionFiscalYearScope(entry, companyId, date)
            : null;
    }

    private static void ApplyFiscalYear(
        TransactionFiscalYearScope scope,
        FiscalYear? fiscalYear)
    {
        if (fiscalYear is null)
        {
            throw new InvalidOperationException(
                $"لا توجد سنة مالية تغطي تاريخ الحركة {scope.Date:yyyy-MM-dd} للشركة رقم {scope.CompanyId}.");
        }
        if (fiscalYear.Status != FiscalYearStatus.Open)
        {
            throw new InvalidOperationException(
                $"السنة المالية '{fiscalYear.Name}' مغلقة ولا تقبل حركات بتاريخ {scope.Date:yyyy-MM-dd}.");
        }

        scope.Entry.Property(nameof(Invoice.FiscalYearId)).CurrentValue =
            fiscalYear.Id;
    }

    private void AssignInvoicePaymentFiscalYears()
    {
        foreach (var entry in InvoicePaymentsToAssign())
        {
            var payment = (InvoicePayment)entry.Entity;
            var fiscalYearId = ChangeTracker.Entries<CashVoucher>()
                .Where(voucher => voucher.Entity.Id == payment.CashVoucherId)
                .Select(voucher => voucher.Entity.FiscalYearId)
                .FirstOrDefault();
            if (fiscalYearId <= 0)
            {
                fiscalYearId = CashVouchers
                    .AsNoTracking()
                    .Where(voucher =>
                        voucher.CompanyId == payment.CompanyId &&
                        voucher.Id == payment.CashVoucherId)
                    .Select(voucher => voucher.FiscalYearId)
                    .Single();
            }
            payment.FiscalYearId = fiscalYearId;
        }
    }

    private async Task AssignInvoicePaymentFiscalYearsAsync(
        CancellationToken cancellationToken)
    {
        foreach (var entry in InvoicePaymentsToAssign())
        {
            var payment = (InvoicePayment)entry.Entity;
            var fiscalYearId = ChangeTracker.Entries<CashVoucher>()
                .Where(voucher => voucher.Entity.Id == payment.CashVoucherId)
                .Select(voucher => voucher.Entity.FiscalYearId)
                .FirstOrDefault();
            if (fiscalYearId <= 0)
            {
                fiscalYearId = await CashVouchers
                    .AsNoTracking()
                    .Where(voucher =>
                        voucher.CompanyId == payment.CompanyId &&
                        voucher.Id == payment.CashVoucherId)
                    .Select(voucher => voucher.FiscalYearId)
                    .SingleAsync(cancellationToken);
            }
            payment.FiscalYearId = fiscalYearId;
        }
    }

    private IEnumerable<EntityEntry> InvoicePaymentsToAssign() =>
        ChangeTracker.Entries<InvoicePayment>()
            .Where(entry =>
                (entry.State == EntityState.Added ||
                 entry.State == EntityState.Modified) &&
                entry.Entity.FiscalYearId <= 0)
            .Cast<EntityEntry>();

    private sealed record TransactionFiscalYearScope(
        EntityEntry Entry,
        int CompanyId,
        DateOnly Date);
}
