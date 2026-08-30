using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Mappings;
using MiniErp.Application.Features.Cashboxes;
using MiniErp.Application.Features.ExchangeRates;
using MiniErp.Domain.Entities.BusinessPartners;
using MiniErp.Domain.Entities.CashManagement;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Entities.Employees;
using MiniErp.Domain.Entities.Invoicing;
using MiniErp.Domain.Entities.Inventory;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Persistence.Interceptors;
using MiniErp.Infrastructure.Services.Cashboxes;
using MiniErp.Infrastructure.Services.ExchangeRates;
using MiniErp.Infrastructure.Services.Pagination;

namespace MiniErp.Tests.CashManagement;

public sealed class CashboxExchangeRateCascadeTests
{
    static CashboxExchangeRateCascadeTests()
    {
        MappingConfiguration.Register(
            typeof(InfrastructureAssemblyMarker).Assembly);
    }

    [Fact]
    public async Task UpdateWithoutFlagChangesOnlyEditedCashboxSnapshot()
    {
        await using var database = await CascadeTestDatabase.CreateAsync();
        var service = database.CreateService();
        var cashbox = await service.GetByIdAsync(database.PrimaryCashboxId);

        var result = await service.UpdateAsync(
            database.PrimaryCashboxId,
            CreateUpdateRequest(
                cashbox.Value,
                openingExchangeRate: 60m,
                updateLinkedTransactions: false));

        Assert.True(result.IsSuccess);
        Assert.Equal(60m, result.Value.OpeningExchangeRate);
        Assert.Equal(6_000m, result.Value.BaseOpeningBalance);

        database.Context.ChangeTracker.Clear();
        var rate = await database.Context.ExchangeRates
            .AsNoTracking()
            .SingleAsync(entity => entity.Id == database.UsdRateId);
        var linkedCashbox = await database.Context.Cashboxes
            .AsNoTracking()
            .SingleAsync(entity => entity.Id == database.LinkedCashboxId);
        var invoice = await database.Context.Invoices
            .AsNoTracking()
            .SingleAsync();
        var voucherBaseAmounts = await database.Context.CashVouchers
            .AsNoTracking()
            .Where(voucher =>
                voucher.ExchangeRateId == database.UsdRateId)
            .Select(voucher => voucher.BaseAmount)
            .ToArrayAsync();

        Assert.Equal(50m, rate.Rate);
        Assert.Equal(50m, linkedCashbox.OpeningExchangeRate);
        Assert.Equal(500m, linkedCashbox.BaseOpeningBalance);
        Assert.Equal(50m, invoice.ExchangeRate);
        Assert.Equal(900m, invoice.BaseTotal);
        Assert.Contains(500m, voucherBaseAmounts);
        Assert.Contains(1_000m, voucherBaseAmounts);
        Assert.Contains(5_500m, voucherBaseAmounts);
    }

    [Fact]
    public async Task UpdateWithFlagCascadesEveryLinkedBaseCurrencyAmount()
    {
        await using var database = await CascadeTestDatabase.CreateAsync();
        var service = database.CreateService();
        var cashbox = await service.GetByIdAsync(database.PrimaryCashboxId);

        var result = await service.UpdateAsync(
            database.PrimaryCashboxId,
            CreateUpdateRequest(
                cashbox.Value,
                openingExchangeRate: 60m,
                updateLinkedTransactions: true));

        Assert.True(result.IsSuccess);
        database.Context.ChangeTracker.Clear();

        var rate = await database.Context.ExchangeRates
            .AsNoTracking()
            .SingleAsync(entity => entity.Id == database.UsdRateId);
        var cashboxes = await database.Context.Cashboxes
            .AsNoTracking()
            .Where(entity =>
                entity.OpeningExchangeRateId == database.UsdRateId)
            .OrderBy(entity => entity.Id)
            .ToArrayAsync();
        var invoice = await database.Context.Invoices
            .AsNoTracking()
            .Include(entity => entity.Lines)
            .SingleAsync();
        var partnerOpening = await database.Context.PartnerOpeningBalances
            .AsNoTracking()
            .SingleAsync();
        var partnerVoucher = await database.Context.CashVouchers
            .AsNoTracking()
            .SingleAsync(voucher =>
                voucher.VoucherNumber == "PARTNER-1");
        var employeeVoucher = await database.Context.CashVouchers
            .AsNoTracking()
            .SingleAsync(voucher =>
                voucher.VoucherNumber == "EMPLOYEE-1");
        var movements = await database.Context.BusinessPartnerMovements
            .AsNoTracking()
            .OrderBy(movement => movement.Id)
            .ToArrayAsync();
        var employeeMovement = await database.Context.EmployeeMovements
            .AsNoTracking()
            .SingleAsync();
        var invoicePayment = await database.Context.InvoicePayments
            .AsNoTracking()
            .SingleAsync();
        var transferVouchers = await database.Context.CashVouchers
            .AsNoTracking()
            .Where(voucher => voucher.CashboxTransferId.HasValue)
            .OrderBy(voucher => voucher.Direction)
            .ToArrayAsync();
        var transferPayment = Assert.Single(transferVouchers, voucher =>
            voucher.Direction == CashDirection.Payment);
        var transferReceipt = Assert.Single(transferVouchers, voucher =>
            voucher.Direction == CashDirection.Receipt);

        Assert.Equal(60m, rate.Rate);
        Assert.All(cashboxes, entity =>
            Assert.Equal(60m, entity.OpeningExchangeRate));
        Assert.Equal(6_000m, cashboxes[0].BaseOpeningBalance);
        Assert.Equal(600m, cashboxes[1].BaseOpeningBalance);

        Assert.Equal(60m, invoice.ExchangeRate);
        Assert.Equal(1_200m, invoice.BaseSubtotal);
        Assert.Equal(120m, invoice.BaseDiscountAmount);
        Assert.Equal(1_080m, invoice.BaseTotal);
        Assert.Equal(300m, invoice.BasePaidAmountAtInvoiceRate);
        var invoiceLine = Assert.Single(invoice.Lines);
        Assert.Equal(600m, invoiceLine.BaseUnitPrice);
        Assert.Equal(1_200m, invoiceLine.BaseTotal);

        Assert.Equal(1_800m, partnerOpening.BaseAmount);
        Assert.Equal(600m, partnerVoucher.BaseAmount);
        Assert.Equal(1_200m, employeeVoucher.BaseAmount);
        Assert.Contains(movements, movement =>
            movement.InvoiceId.HasValue &&
            movement.BaseDebit == 1_080m);
        Assert.Contains(movements, movement =>
            movement.CashVoucherId == partnerVoucher.Id &&
            movement.BaseCredit == 600m);
        Assert.Equal(1_200m, employeeMovement.BaseDebit);

        Assert.Equal(300m, invoicePayment.AppliedBaseAmount);
        Assert.Equal(275m, invoicePayment.CashboxBaseAmount);
        Assert.Equal(-25m, invoicePayment.RealizedExchangeDifference);

        Assert.Equal(60m, transferPayment.ExchangeRate);
        Assert.Equal(6_600m, transferPayment.BaseAmount);
        Assert.Equal(120m, transferReceipt.Amount);
        Assert.Equal(6_600m, transferReceipt.BaseAmount);
    }

    [Fact]
    public async Task ExchangeRateUpdateWithFlagCascadesEveryLinkedBaseCurrencyAmount()
    {
        await using var database = await CascadeTestDatabase.CreateAsync();
        var service = database.CreateExchangeRateService();
        var rate = await database.Context.ExchangeRates
            .AsNoTracking()
            .SingleAsync(entity => entity.Id == database.UsdRateId);

        var result = await service.UpdateAsync(
            database.UsdRateId,
            new ExchangeRateUpdateRequest(
                Currency: rate.Currency,
                RateDate: rate.RateDate,
                Rate: 60m,
                Source: rate.Source,
                Notes: rate.Notes,
                RowVersion: rate.RowVersion,
                UpdateLinkedTransactions: true));

        Assert.True(result.IsSuccess);
        database.Context.ChangeTracker.Clear();

        var invoice = await database.Context.Invoices
            .AsNoTracking()
            .SingleAsync();
        var cashbox = await database.Context.Cashboxes
            .AsNoTracking()
            .SingleAsync(entity => entity.Id == database.PrimaryCashboxId);
        var voucherBaseAmounts = await database.Context.CashVouchers
            .AsNoTracking()
            .Where(voucher => voucher.ExchangeRateId == database.UsdRateId)
            .Select(voucher => voucher.BaseAmount)
            .ToArrayAsync();

        Assert.Equal(60m, result.Value.Rate);
        Assert.Equal(60m, invoice.ExchangeRate);
        Assert.Equal(1_080m, invoice.BaseTotal);
        Assert.Equal(6_000m, cashbox.BaseOpeningBalance);
        Assert.Contains(600m, voucherBaseAmounts);
        Assert.Contains(1_200m, voucherBaseAmounts);
    }

    [Fact]
    public async Task InvalidTransferPairRollsBackEntireCascade()
    {
        await using var database = await CascadeTestDatabase.CreateAsync();
        var receipt = await database.Context.CashVouchers
            .SingleAsync(voucher =>
                voucher.CashboxTransferId.HasValue &&
                voucher.Direction == CashDirection.Receipt);
        receipt.CashboxTransferId = null;
        receipt.CashboxTransfer = null;
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();

        var service = database.CreateService();
        var cashbox = await service.GetByIdAsync(database.PrimaryCashboxId);
        var result = await service.UpdateAsync(
            database.PrimaryCashboxId,
            CreateUpdateRequest(
                cashbox.Value,
                openingExchangeRate: 60m,
                updateLinkedTransactions: true));

        Assert.True(result.IsFailure);
        Assert.Equal("Cashboxes.InvalidLinkedTransfer", result.Error.Code);
        database.Context.ChangeTracker.Clear();

        var rate = await database.Context.ExchangeRates
            .AsNoTracking()
            .SingleAsync(entity => entity.Id == database.UsdRateId);
        var storedCashbox = await database.Context.Cashboxes
            .AsNoTracking()
            .SingleAsync(entity => entity.Id == database.PrimaryCashboxId);
        var invoice = await database.Context.Invoices
            .AsNoTracking()
            .SingleAsync();

        Assert.Equal(50m, rate.Rate);
        Assert.Equal(50m, storedCashbox.OpeningExchangeRate);
        Assert.Equal(5_000m, storedCashbox.BaseOpeningBalance);
        Assert.Equal(50m, invoice.ExchangeRate);
        Assert.Equal(900m, invoice.BaseTotal);
    }

    private static CashboxUpdateRequest CreateUpdateRequest(
        CashboxResponse cashbox,
        decimal openingExchangeRate,
        bool updateLinkedTransactions) =>
        new(
            Name: cashbox.Name,
            Currency: cashbox.Currency,
            OpeningBalance: cashbox.OpeningBalance,
            IsActive: cashbox.IsActive,
            Notes: cashbox.Notes,
            RowVersion: cashbox.RowVersion,
            OpeningBalanceDate: cashbox.OpeningBalanceDate,
            OpeningExchangeRate: openingExchangeRate,
            UpdateLinkedTransactions: updateLinkedTransactions);

    private sealed class CascadeTestDatabase : IAsyncDisposable
    {
        private CascadeTestDatabase(
            SqliteConnection connection,
            ApplicationDbContext context,
            int usdRateId,
            int primaryCashboxId,
            int linkedCashboxId)
        {
            Connection = connection;
            Context = context;
            UsdRateId = usdRateId;
            PrimaryCashboxId = primaryCashboxId;
            LinkedCashboxId = linkedCashboxId;
        }

        private SqliteConnection Connection { get; }

        public ApplicationDbContext Context { get; }

        public int UsdRateId { get; }

        public int PrimaryCashboxId { get; }

        public int LinkedCashboxId { get; }

        public static async Task<CascadeTestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .AddInterceptors(new AuditableEntityInterceptor(
                    new HttpContextAccessor(),
                    TimeProvider.System))
                .Options;
            var context = new ApplicationDbContext(options);
            var createScript = context.Database.GenerateCreateScript()
                .Replace(
                    "CONSTRAINT \"CK_BusinessPartnerMovements_Amounts_NonNegative\" CHECK ([Debit] >= 0 AND [Credit] >= 0),",
                    string.Empty,
                    StringComparison.Ordinal)
                .Replace(
                    "CONSTRAINT \"CK_BusinessPartnerMovements_ExactlyOneAmount\" CHECK (([Debit] > 0 AND [Credit] = 0) OR ([Debit] = 0 AND [Credit] > 0)),",
                    string.Empty,
                    StringComparison.Ordinal)
                .Replace(
                    "CONSTRAINT \"CK_EmployeeMovements_Amounts_NonNegative\" CHECK ([Debit] >= 0 AND [Credit] >= 0),",
                    string.Empty,
                    StringComparison.Ordinal)
                .Replace(
                    "CONSTRAINT \"CK_EmployeeMovements_ExactlyOneAmount\" CHECK (([Debit] > 0 AND [Credit] = 0) OR ([Debit] = 0 AND [Credit] > 0)),",
                    string.Empty,
                    StringComparison.Ordinal)
                .Replace(
                    "\"RowVersion\" BLOB NOT NULL",
                    "\"RowVersion\" BLOB NOT NULL DEFAULT (randomblob(8))",
                    StringComparison.Ordinal);
            await context.Database.ExecuteSqlRawAsync(createScript);
            await context.Database.ExecuteSqlRawAsync(
                """
                CREATE TRIGGER AdvanceCascadeCashboxRowVersion
                AFTER UPDATE ON Cashboxes
                BEGIN
                    UPDATE Cashboxes SET RowVersion = randomblob(8)
                    WHERE Id = NEW.Id;
                END;
                CREATE TRIGGER AdvanceCascadeExchangeRateRowVersion
                AFTER UPDATE ON ExchangeRates
                BEGIN
                    UPDATE ExchangeRates SET RowVersion = randomblob(8)
                    WHERE Id = NEW.Id;
                END;
                """);

            var company = new Company
            {
                Name = "Cascade Company",
                Address = string.Empty,
                CommercialRegister = "CR-CASCADE",
                TaxNumber = "TX-CASCADE",
                ManagerName = "Manager"
            };
            context.Companies.Add(company);
            await context.SaveChangesAsync();
            context.CompanySettings.Add(new CompanySettings
            {
                CompanyId = company.Id,
                Company = company,
                BaseCurrency = CurrencyCode.EGP,
                StockBalanceCheckMode = StockBalanceCheckMode.None
            });

            var usdRate = CreateRate(company.Id, CurrencyCode.USD, 50m);
            var eurRate = CreateRate(company.Id, CurrencyCode.EUR, 55m);
            context.ExchangeRates.AddRange(usdRate, eurRate);
            await context.SaveChangesAsync();

            var primaryCashbox = CreateCashbox(
                company.Id,
                "CBX-USD-1",
                "Primary USD",
                CurrencyCode.USD,
                openingBalance: 100m,
                exchangeRateId: usdRate.Id,
                exchangeRate: usdRate.Rate);
            var linkedCashbox = CreateCashbox(
                company.Id,
                "CBX-USD-2",
                "Linked USD",
                CurrencyCode.USD,
                openingBalance: 10m,
                exchangeRateId: usdRate.Id,
                exchangeRate: usdRate.Rate);
            var eurCashbox = CreateCashbox(
                company.Id,
                "CBX-EUR-1",
                "EUR",
                CurrencyCode.EUR,
                openingBalance: 0m,
                exchangeRateId: eurRate.Id,
                exchangeRate: eurRate.Rate);
            context.Cashboxes.AddRange(
                primaryCashbox,
                linkedCashbox,
                eurCashbox);

            var partner = new BusinessPartner
            {
                CompanyId = company.Id,
                Code = "BP-1",
                Name = "Partner",
                Currency = CurrencyCode.USD,
                IsActive = true
            };
            var employee = new Employee
            {
                CompanyId = company.Id,
                Code = "EMP-1",
                Name = "Employee",
                Type = EmployeeType.Monthly,
                MonthlySalary = 1_000m,
                IsActive = true
            };
            var store = new Store
            {
                CompanyId = company.Id,
                Code = "STORE-1",
                Name = "Store",
                IsActive = true
            };
            context.AddRange(partner, employee, store);
            await context.SaveChangesAsync();

            var invoiceLine = new InvoiceLine
            {
                CompanyId = company.Id,
                ItemName = "Service",
                Count = 1,
                Weight = 2m,
                Price = 10m
            };
            var invoice = new Invoice
            {
                CompanyId = company.Id,
                InvoiceNumber = "INV-USD-1",
                InvoiceType = InvoiceType.Sales,
                ContentType = InvoiceContentType.Items,
                PaymentTerm = PaymentTerm.Cash,
                InvoiceDate = new DateOnly(2026, 8, 30),
                BusinessPartnerId = partner.Id,
                StoreId = store.Id,
                Currency = CurrencyCode.USD,
                DiscountAmount = 2m,
                PaidAmount = 5m,
                Lines = [invoiceLine]
            };
            invoice.CalculateTotal();
            invoice.ApplyExchangeRate(usdRate.Id, usdRate.Rate);
            invoice.Touch(DateTime.UtcNow);
            context.Invoices.Add(invoice);

            var partnerOpening = new PartnerOpeningBalance
            {
                CompanyId = company.Id,
                BusinessPartnerId = partner.Id,
                DocumentNumber = "POB-1",
                DocumentDate = new DateOnly(2026, 8, 30),
                Currency = CurrencyCode.USD,
                BalanceType = PartnerBalanceType.Receivable,
                Amount = 30m
            };
            partnerOpening.ApplyExchangeRate(usdRate.Id, usdRate.Rate);
            context.Add(partnerOpening);

            var partnerVoucher = CreateVoucher(
                company.Id,
                "PARTNER-1",
                primaryCashbox,
                CashDirection.Receipt,
                CashPartyType.Partner,
                amount: 10m,
                exchangeRateId: usdRate.Id,
                exchangeRate: usdRate.Rate);
            partnerVoucher.BusinessPartnerId = partner.Id;
            var employeeVoucher = CreateVoucher(
                company.Id,
                "EMPLOYEE-1",
                primaryCashbox,
                CashDirection.Payment,
                CashPartyType.Employee,
                amount: 20m,
                exchangeRateId: usdRate.Id,
                exchangeRate: usdRate.Rate);
            employeeVoucher.EmployeeId = employee.Id;
            context.CashVouchers.AddRange(partnerVoucher, employeeVoucher);

            var transfer = new CashboxTransfer
            {
                CompanyId = company.Id,
                TransferNumber = "TRF-1",
                TransferDate = new DateOnly(2026, 8, 30),
                SourceCashbox = primaryCashbox,
                DestinationCashbox = eurCashbox
            };
            transfer.Touch(DateTime.UtcNow);
            var transferPayment = CreateVoucher(
                company.Id,
                "TRF-PAY-1",
                primaryCashbox,
                CashDirection.Payment,
                CashPartyType.None,
                amount: 110m,
                exchangeRateId: usdRate.Id,
                exchangeRate: usdRate.Rate);
            transferPayment.CashboxTransfer = transfer;
            var transferReceipt = CreateVoucher(
                company.Id,
                "TRF-REC-1",
                eurCashbox,
                CashDirection.Receipt,
                CashPartyType.None,
                amount: 100m,
                exchangeRateId: eurRate.Id,
                exchangeRate: eurRate.Rate);
            transferReceipt.CashboxTransfer = transfer;
            context.CashboxTransfers.Add(transfer);
            context.CashVouchers.AddRange(transferPayment, transferReceipt);

            var invoiceVoucher = CreateVoucher(
                company.Id,
                "INV-PAY-1",
                eurCashbox,
                CashDirection.Receipt,
                CashPartyType.Partner,
                amount: 5m,
                exchangeRateId: eurRate.Id,
                exchangeRate: eurRate.Rate);
            invoiceVoucher.Invoice = invoice;
            invoiceVoucher.BusinessPartnerId = partner.Id;
            context.CashVouchers.Add(invoiceVoucher);
            await context.SaveChangesAsync();

            var invoiceMovement = new BusinessPartnerMovement
            {
                CompanyId = company.Id,
                BusinessPartnerId = partner.Id,
                InvoiceId = invoice.Id,
                MovementType = BusinessPartnerMovementType.Sales,
                MovementDate = invoice.InvoiceDate,
                Currency = invoice.Currency,
                Debit = invoice.Total
            };
            invoiceMovement.ApplyExchangeRate(invoice.ExchangeRate);
            var invoicePaymentMovement = new BusinessPartnerMovement
            {
                CompanyId = company.Id,
                BusinessPartnerId = partner.Id,
                CashVoucherId = invoiceVoucher.Id,
                MovementType = BusinessPartnerMovementType.CashReceipt,
                MovementDate = invoice.InvoiceDate,
                Currency = invoice.Currency,
                Credit = invoice.PaidAmount
            };
            invoicePaymentMovement.ApplyExchangeRate(invoice.ExchangeRate);
            var voucherMovement = new BusinessPartnerMovement
            {
                CompanyId = company.Id,
                BusinessPartnerId = partner.Id,
                CashVoucherId = partnerVoucher.Id,
                MovementType = BusinessPartnerMovementType.CashReceipt,
                MovementDate = partnerVoucher.VoucherDate,
                Currency = partnerVoucher.Currency,
                Credit = partnerVoucher.Amount
            };
            voucherMovement.ApplyExchangeRate(partnerVoucher.ExchangeRate);
            var employeeMovement = new EmployeeMovement
            {
                CompanyId = company.Id,
                EmployeeId = employee.Id,
                CashVoucherId = employeeVoucher.Id,
                Type = EmployeeMovementType.Advance,
                MovementDate = employeeVoucher.VoucherDate,
                Currency = employeeVoucher.Currency,
                Debit = employeeVoucher.Amount
            };
            employeeMovement.ApplyExchangeRate(employeeVoucher.ExchangeRate);
            var payment = new InvoicePayment
            {
                CompanyId = company.Id,
                InvoiceId = invoice.Id,
                CashVoucherId = invoiceVoucher.Id
            };
            payment.Apply(
                invoiceCurrency: invoice.Currency,
                appliedAmount: invoice.PaidAmount,
                cashboxCurrency: invoiceVoucher.Currency,
                cashboxAmount: invoiceVoucher.Amount,
                invoiceToBaseRate: invoice.ExchangeRate,
                cashboxToBaseRate: invoiceVoucher.ExchangeRate);
            context.AddRange(
                invoiceMovement,
                invoicePaymentMovement,
                voucherMovement,
                employeeMovement,
                payment);
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();

            return new CascadeTestDatabase(
                connection,
                context,
                usdRate.Id,
                primaryCashbox.Id,
                linkedCashbox.Id);
        }

        public CashboxService CreateService()
        {
            var companyId = Context.Companies
                .AsNoTracking()
                .Select(company => company.Id)
                .Single();
            var companyContext = new TestCurrentCompanyContext(companyId);
            return new CashboxService(
                Context,
                new PaginationService(),
                companyContext,
                new ExchangeRateResolver(
                    Context,
                    companyContext,
                    TimeProvider.System),
                TimeProvider.System);
        }

        public ExchangeRateService CreateExchangeRateService()
        {
            var companyId = Context.Companies
                .AsNoTracking()
                .Select(company => company.Id)
                .Single();
            var companyContext = new TestCurrentCompanyContext(companyId);
            return new ExchangeRateService(
                Context,
                new PaginationService(),
                companyContext,
                TimeProvider.System,
                new ExchangeRateResolver(
                    Context,
                    companyContext,
                    TimeProvider.System));
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await Connection.DisposeAsync();
        }

        private static ExchangeRate CreateRate(
            int companyId,
            CurrencyCode currency,
            decimal rate)
        {
            var entity = new ExchangeRate
            {
                CompanyId = companyId,
                Currency = currency,
                RateDate = new DateOnly(2026, 8, 30),
                Rate = rate,
                Source = ExchangeRateSource.Manual
            };
            entity.Touch(DateTime.UtcNow);
            return entity;
        }

        private static Cashbox CreateCashbox(
            int companyId,
            string code,
            string name,
            CurrencyCode currency,
            decimal openingBalance,
            int exchangeRateId,
            decimal exchangeRate)
        {
            var cashbox = new Cashbox
            {
                CompanyId = companyId,
                Code = code,
                Name = name,
                Currency = currency,
                OpeningBalance = openingBalance,
                IsActive = true
            };
            cashbox.ApplyOpeningExchangeRate(
                new DateOnly(2026, 8, 30),
                exchangeRateId,
                exchangeRate);
            return cashbox;
        }

        private static CashVoucher CreateVoucher(
            int companyId,
            string number,
            Cashbox cashbox,
            CashDirection direction,
            CashPartyType partyType,
            decimal amount,
            int exchangeRateId,
            decimal exchangeRate)
        {
            var voucher = new CashVoucher
            {
                CompanyId = companyId,
                VoucherNumber = number,
                VoucherDate = new DateOnly(2026, 8, 30),
                Direction = direction,
                Cashbox = cashbox,
                PartyType = partyType,
                Amount = amount,
                Currency = cashbox.Currency,
                IsPosted = true
            };
            voucher.ApplyExchangeRate(exchangeRateId, exchangeRate);
            voucher.Touch(DateTime.UtcNow);
            return voucher;
        }
    }

    private sealed record TestCurrentCompanyContext(int CompanyId)
        : ICurrentCompanyContext;
}
