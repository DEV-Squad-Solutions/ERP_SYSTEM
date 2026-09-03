using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Features.Companies;
using MiniErp.Domain.Entities.Accounting;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.AccountingSetup;

public sealed class DefaultAccountingSetupService(
    ApplicationDbContext dbContext)
    : IDefaultAccountingSetupService, IScopedService
{
    private static readonly AccountSeed[] AccountSeeds =
    [
        new("1000", "الأصول", null, AccountType.Asset, NormalBalance.Debit, false),
        new("1100", "النقدية وما في حكمها", "1000", AccountType.Asset, NormalBalance.Debit, false),
        new("1110", "الخزائن", "1100", AccountType.Asset, NormalBalance.Debit, true),
        new("1200", "العملاء", "1000", AccountType.Asset, NormalBalance.Debit, true),
        new("1300", "المخزون", "1000", AccountType.Asset, NormalBalance.Debit, true),
        new("1400", "ذمم الموظفين المدينة", "1000", AccountType.Asset, NormalBalance.Debit, true),
        new("2000", "الالتزامات", null, AccountType.Liability, NormalBalance.Credit, false),
        new("2100", "الموردون", "2000", AccountType.Liability, NormalBalance.Credit, true),
        new("2200", "مستحقات الموظفين والسائقين", "2000", AccountType.Liability, NormalBalance.Credit, true),
        new("3000", "حقوق الملكية", null, AccountType.Equity, NormalBalance.Credit, false),
        new("3100", "رأس المال", "3000", AccountType.Equity, NormalBalance.Credit, true),
        new("3200", "مقابل الأرصدة الافتتاحية", "3000", AccountType.Equity, NormalBalance.Credit, true),
        new("4000", "الإيرادات", null, AccountType.Revenue, NormalBalance.Credit, false),
        new("4100", "إيرادات المبيعات", "4000", AccountType.Revenue, NormalBalance.Credit, true),
        new("4200", "إيرادات أخرى", "4000", AccountType.Revenue, NormalBalance.Credit, true),
        new("4300", "أرباح فروق العملات", "4000", AccountType.Revenue, NormalBalance.Credit, true),
        new("4400", "أرباح زيادة المخزون", "4000", AccountType.Revenue, NormalBalance.Credit, true),
        new("5000", "المصروفات", null, AccountType.Expense, NormalBalance.Debit, false),
        new("5100", "تكلفة المبيعات", "5000", AccountType.Expense, NormalBalance.Debit, true),
        new("5200", "مصروفات التشغيل", "5000", AccountType.Expense, NormalBalance.Debit, true),
        new("5300", "مصروفات إدارية", "5000", AccountType.Expense, NormalBalance.Debit, true),
        new("5400", "خسائر فروق العملات", "5000", AccountType.Expense, NormalBalance.Debit, true),
        new("5500", "خسائر عجز المخزون", "5000", AccountType.Expense, NormalBalance.Debit, true)
    ];

    private static readonly MappingSeed[] GeneralMappingSeeds =
    [
        new(AccountingMappingType.Sales, "4100"),
        new(AccountingMappingType.Purchase, "1300"),
        new(AccountingMappingType.SalesReturn, "4200"),
        new(AccountingMappingType.PurchaseReturn, "1300"),
        new(AccountingMappingType.Inventory, "1300"),
        new(AccountingMappingType.CostOfGoodsSold, "5100"),
        new(AccountingMappingType.CustomerControl, "1200"),
        new(AccountingMappingType.SupplierControl, "2100"),
        new(AccountingMappingType.EmployeeControl, "2200"),
        new(AccountingMappingType.DriverControl, "2200"),
        new(AccountingMappingType.ExchangeGain, "4300"),
        new(AccountingMappingType.ExchangeLoss, "5400"),
        new(AccountingMappingType.InventoryAdjustmentGain, "4400"),
        new(AccountingMappingType.InventoryAdjustmentLoss, "5500"),
        new(AccountingMappingType.OpeningBalanceEquity, "3200"),
        new(AccountingMappingType.EmployeeReceivable, "1400"),
        new(AccountingMappingType.DriverTripExpense, "5200")
    ];

    private static readonly StatementSeed[] StatementSeeds =
    [
        new(
            FinancialStatementType.FinancialPosition,
            [
                new("FP-100", "الأصول", null, 100, false),
                new("FP-110", "النقدية وما في حكمها", "FP-100", 110, true),
                new("FP-120", "العملاء", "FP-100", 120, true),
                new("FP-130", "المخزون", "FP-100", 130, true),
                new("FP-140", "ذمم الموظفين المدينة", "FP-100", 140, true),
                new("FP-200", "الالتزامات", null, 200, false),
                new("FP-210", "الموردون", "FP-200", 210, true),
                new("FP-220", "مستحقات الموظفين والسائقين", "FP-200", 220, true),
                new("FP-300", "حقوق الملكية", null, 300, false),
                new("FP-310", "رأس المال", "FP-300", 310, true),
                new("FP-320", "مقابل الأرصدة الافتتاحية", "FP-300", 320, true)
            ],
            [
                new("1110", "FP-110"), new("1200", "FP-120"),
                new("1300", "FP-130"), new("1400", "FP-140"),
                new("2100", "FP-210"), new("2200", "FP-220"),
                new("3100", "FP-310"), new("3200", "FP-320")
            ]),
        new(
            FinancialStatementType.IncomeStatement,
            [
                new("IS-100", "الإيرادات", null, 100, false),
                new("IS-110", "إيرادات المبيعات", "IS-100", 110, true),
                new("IS-120", "إيرادات أخرى", "IS-100", 120, true),
                new("IS-130", "أرباح فروق العملات", "IS-100", 130, true),
                new("IS-140", "أرباح زيادة المخزون", "IS-100", 140, true),
                new("IS-200", "المصروفات", null, 200, false),
                new("IS-210", "تكلفة المبيعات", "IS-200", 210, true),
                new("IS-220", "مصروفات التشغيل", "IS-200", 220, true),
                new("IS-230", "مصروفات إدارية", "IS-200", 230, true),
                new("IS-240", "خسائر فروق العملات", "IS-200", 240, true),
                new("IS-250", "خسائر عجز المخزون", "IS-200", 250, true)
            ],
            [
                new("4100", "IS-110"), new("4200", "IS-120"),
                new("4300", "IS-130"), new("4400", "IS-140"),
                new("5100", "IS-210"), new("5200", "IS-220"),
                new("5300", "IS-230"), new("5400", "IS-240"),
                new("5500", "IS-250")
            ]),
        new(
            FinancialStatementType.CashFlow,
            [
                new("CF-100", "الأنشطة التشغيلية", null, 100, false),
                new("CF-110", "متحصلات العملاء والمبيعات", "CF-100", 110, true),
                new("CF-120", "مدفوعات الموردين والمخزون", "CF-100", 120, true),
                new("CF-130", "مدفوعات التشغيل والإدارة", "CF-100", 130, true),
                new("CF-140", "تدفقات تشغيلية أخرى", "CF-100", 140, true),
                new("CF-200", "الأنشطة الاستثمارية", null, 200, false),
                new("CF-210", "شراء أصول ثابتة", "CF-200", 210, true),
                new("CF-220", "بيع أصول ثابتة", "CF-200", 220, true),
                new("CF-300", "الأنشطة التمويلية", null, 300, false),
                new("CF-310", "مساهمات وزيادات رأس المال", "CF-300", 310, true),
                new("CF-320", "متحصلات القروض", "CF-300", 320, true),
                new("CF-330", "سداد القروض والتوزيعات", "CF-300", 330, true)
            ],
            [
                new("1200", "CF-110"), new("4100", "CF-110"),
                new("1300", "CF-120"), new("2100", "CF-120"),
                new("2200", "CF-130"), new("5100", "CF-130"),
                new("5200", "CF-130"), new("5300", "CF-130"),
                new("1400", "CF-140"), new("4200", "CF-140"),
                new("4300", "CF-140"), new("4400", "CF-140"),
                new("5400", "CF-140"), new("5500", "CF-140"),
                new("3100", "CF-310"), new("3200", "CF-310")
            ])
    ];

    public async Task InitializeCompanyAsync(
        int companyId,
        DateOnly effectiveDate,
        CancellationToken cancellationToken = default)
    {
        var accounts = await EnsureChartOfAccountsAsync(
            companyId,
            cancellationToken);
        var fiscalYear = await EnsureCurrentFiscalYearAsync(
            companyId,
            effectiveDate,
            cancellationToken);

        await EnsureFiscalYearSetupAsync(
            companyId,
            fiscalYear.Id,
            accounts,
            cancellationToken);
    }

    public async Task EnsureFiscalYearAsync(
        int companyId,
        int fiscalYearId,
        CancellationToken cancellationToken = default)
    {
        var fiscalYearExists = await dbContext.FiscalYears.AnyAsync(
            year => year.CompanyId == companyId && year.Id == fiscalYearId,
            cancellationToken);
        if (!fiscalYearExists)
        {
            throw new InvalidOperationException(
                $"Fiscal year {fiscalYearId} does not belong to company {companyId}.");
        }

        var accounts = await EnsureChartOfAccountsAsync(
            companyId,
            cancellationToken);
        await EnsureFiscalYearSetupAsync(
            companyId,
            fiscalYearId,
            accounts,
            cancellationToken);
    }

    public async Task EnsureCashboxAsync(
        int companyId,
        int cashboxId,
        CancellationToken cancellationToken = default)
    {
        var exists = await dbContext.Cashboxes.AnyAsync(
            cashbox =>
                cashbox.CompanyId == companyId &&
                cashbox.Id == cashboxId,
            cancellationToken);
        if (!exists)
        {
            throw new InvalidOperationException(
                $"Cashbox {cashboxId} does not belong to company {companyId}.");
        }

        var accounts = await EnsureChartOfAccountsAsync(
            companyId,
            cancellationToken);
        var fiscalYearIds = await dbContext.FiscalYears
            .Where(year => year.CompanyId == companyId)
            .Select(year => year.Id)
            .ToListAsync(cancellationToken);

        foreach (var fiscalYearId in fiscalYearIds)
        {
            await EnsureMappingAsync(
                companyId,
                fiscalYearId,
                AccountingMappingType.Cashbox,
                cashboxId,
                accounts["1110"].Id,
                cancellationToken);
        }
    }

    public async Task EnsureCashMovementTypeAsync(
        int companyId,
        int cashMovementTypeId,
        CancellationToken cancellationToken = default)
    {
        var movementType = await dbContext.CashMovementTypes
            .Where(entity =>
                entity.CompanyId == companyId &&
                entity.Id == cashMovementTypeId)
            .Select(entity => new
            {
                entity.Classification,
                entity.Direction
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (movementType is null)
        {
            throw new InvalidOperationException(
                $"Cash movement type {cashMovementTypeId} does not belong to company {companyId}.");
        }

        var accountCode = movementType.Classification switch
        {
            CashMovementClassification.Expense => "5200",
            CashMovementClassification.Revenue => "4200",
            CashMovementClassification.PartnerSettlement =>
                movementType.Direction == CashDirection.Payment
                    ? "2100"
                    : "1200",
            _ => "5200"
        };
        var accounts = await EnsureChartOfAccountsAsync(
            companyId,
            cancellationToken);
        var fiscalYearIds = await dbContext.FiscalYears
            .Where(year => year.CompanyId == companyId)
            .Select(year => year.Id)
            .ToListAsync(cancellationToken);

        foreach (var fiscalYearId in fiscalYearIds)
        {
            await EnsureMappingAsync(
                companyId,
                fiscalYearId,
                AccountingMappingType.CashMovementType,
                cashMovementTypeId,
                accounts[accountCode].Id,
                cancellationToken);
        }
    }

    private async Task<IReadOnlyDictionary<string, Account>>
        EnsureChartOfAccountsAsync(
            int companyId,
            CancellationToken cancellationToken)
    {
        var accounts = await dbContext.Accounts
            .Where(account => account.CompanyId == companyId)
            .ToDictionaryAsync(
                account => account.Code,
                StringComparer.OrdinalIgnoreCase,
                cancellationToken);
        var pending = AccountSeeds
            .Where(seed => !accounts.ContainsKey(seed.Code))
            .ToList();

        while (pending.Count > 0)
        {
            var ready = pending
                .Where(seed =>
                    seed.ParentCode is null ||
                    accounts.ContainsKey(seed.ParentCode))
                .ToList();
            if (ready.Count == 0)
            {
                throw new InvalidOperationException(
                    "The default chart of accounts contains an invalid parent reference.");
            }

            foreach (var seed in ready)
            {
                var account = new Account
                {
                    CompanyId = companyId,
                    Code = seed.Code,
                    Name = seed.Name,
                    ParentAccountId = seed.ParentCode is null
                        ? null
                        : accounts[seed.ParentCode].Id,
                    AccountType = seed.AccountType,
                    NormalBalance = seed.NormalBalance,
                    IsPosting = seed.IsPosting,
                    IsActive = true
                };
                dbContext.Accounts.Add(account);
                accounts.Add(seed.Code, account);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            pending.RemoveAll(seed => ready.Contains(seed));
        }

        return accounts;
    }

    private async Task<FiscalYear> EnsureCurrentFiscalYearAsync(
        int companyId,
        DateOnly effectiveDate,
        CancellationToken cancellationToken)
    {
        var current = await dbContext.FiscalYears
            .Where(year => year.CompanyId == companyId && year.IsCurrent)
            .SingleOrDefaultAsync(cancellationToken);
        if (current is not null)
        {
            return current;
        }

        current = new FiscalYear
        {
            CompanyId = companyId,
            Name = effectiveDate.Year.ToString(),
            StartDate = new DateOnly(effectiveDate.Year, 1, 1),
            EndDate = new DateOnly(effectiveDate.Year, 12, 31),
            Status = FiscalYearStatus.Open,
            IsCurrent = true
        };
        dbContext.FiscalYears.Add(current);
        await dbContext.SaveChangesAsync(cancellationToken);
        return current;
    }

    private async Task EnsureFiscalYearSetupAsync(
        int companyId,
        int fiscalYearId,
        IReadOnlyDictionary<string, Account> accounts,
        CancellationToken cancellationToken)
    {
        foreach (var seed in GeneralMappingSeeds)
        {
            await EnsureMappingAsync(
                companyId,
                fiscalYearId,
                seed.MappingType,
                sourceId: null,
                accounts[seed.AccountCode].Id,
                cancellationToken);
        }

        var cashboxIds = await dbContext.Cashboxes
            .Where(cashbox => cashbox.CompanyId == companyId)
            .Select(cashbox => cashbox.Id)
            .ToListAsync(cancellationToken);
        foreach (var cashboxId in cashboxIds)
        {
            await EnsureMappingAsync(
                companyId,
                fiscalYearId,
                AccountingMappingType.Cashbox,
                cashboxId,
                accounts["1110"].Id,
                cancellationToken);
        }

        var movementTypes = await dbContext.CashMovementTypes
            .Where(movementType => movementType.CompanyId == companyId)
            .Select(movementType => new
            {
                movementType.Id,
                movementType.Classification,
                movementType.Direction
            })
            .ToListAsync(cancellationToken);
        foreach (var movementType in movementTypes)
        {
            var accountCode = movementType.Classification switch
            {
                CashMovementClassification.Expense => "5200",
                CashMovementClassification.Revenue => "4200",
                CashMovementClassification.PartnerSettlement =>
                    movementType.Direction == CashDirection.Payment
                        ? "2100"
                        : "1200",
                _ => "5200"
            };
            await EnsureMappingAsync(
                companyId,
                fiscalYearId,
                AccountingMappingType.CashMovementType,
                movementType.Id,
                accounts[accountCode].Id,
                cancellationToken);
        }

        foreach (var statement in StatementSeeds)
        {
            await EnsureStatementAsync(
                companyId,
                fiscalYearId,
                statement,
                accounts,
                cancellationToken);
        }
    }

    private async Task EnsureMappingAsync(
        int companyId,
        int fiscalYearId,
        AccountingMappingType mappingType,
        int? sourceId,
        int accountId,
        CancellationToken cancellationToken)
    {
        var exists = await dbContext.AccountMappings.AnyAsync(
            mapping =>
                mapping.CompanyId == companyId &&
                mapping.FiscalYearId == fiscalYearId &&
                mapping.MappingType == mappingType &&
                mapping.SourceId == sourceId,
            cancellationToken);
        if (exists)
        {
            return;
        }

        dbContext.AccountMappings.Add(new AccountMapping
        {
            CompanyId = companyId,
            FiscalYearId = fiscalYearId,
            MappingType = mappingType,
            SourceId = sourceId,
            AccountId = accountId
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureStatementAsync(
        int companyId,
        int fiscalYearId,
        StatementSeed statement,
        IReadOnlyDictionary<string, Account> accounts,
        CancellationToken cancellationToken)
    {
        var lines = await dbContext.FinancialStatementLines
            .Where(line =>
                line.CompanyId == companyId &&
                line.FiscalYearId == fiscalYearId &&
                line.StatementType == statement.StatementType)
            .ToDictionaryAsync(
                line => line.Code,
                StringComparer.OrdinalIgnoreCase,
                cancellationToken);
        var pending = statement.Lines
            .Where(seed => !lines.ContainsKey(seed.Code))
            .ToList();

        while (pending.Count > 0)
        {
            var ready = pending
                .Where(seed =>
                    seed.ParentCode is null ||
                    lines.ContainsKey(seed.ParentCode))
                .ToList();
            if (ready.Count == 0)
            {
                throw new InvalidOperationException(
                    "The default financial statement setup contains an invalid parent reference.");
            }

            foreach (var seed in ready)
            {
                var line = new FinancialStatementLine
                {
                    CompanyId = companyId,
                    FiscalYearId = fiscalYearId,
                    StatementType = statement.StatementType,
                    Code = seed.Code,
                    Name = seed.Name,
                    ParentLineId = seed.ParentCode is null
                        ? null
                        : lines[seed.ParentCode].Id,
                    DisplayOrder = seed.DisplayOrder,
                    IsAssignable = seed.IsAssignable,
                    IsActive = true
                };
                dbContext.FinancialStatementLines.Add(line);
                lines.Add(seed.Code, line);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            pending.RemoveAll(seed => ready.Contains(seed));
        }

        var mappedAccountIds = (await dbContext.AccountStatementMappings
                .Where(mapping =>
                    mapping.CompanyId == companyId &&
                    mapping.FiscalYearId == fiscalYearId &&
                    mapping.StatementType == statement.StatementType)
                .Select(mapping => mapping.AccountId)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        foreach (var seed in statement.Mappings)
        {
            var account = accounts[seed.AccountCode];
            if (!mappedAccountIds.Add(account.Id))
            {
                continue;
            }

            dbContext.AccountStatementMappings.Add(
                new AccountStatementMapping
                {
                    CompanyId = companyId,
                    FiscalYearId = fiscalYearId,
                    StatementType = statement.StatementType,
                    AccountId = account.Id,
                    FinancialStatementLineId = lines[seed.LineCode].Id
                });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private sealed record AccountSeed(
        string Code,
        string Name,
        string? ParentCode,
        AccountType AccountType,
        NormalBalance NormalBalance,
        bool IsPosting);

    private sealed record MappingSeed(
        AccountingMappingType MappingType,
        string AccountCode);

    private sealed record StatementSeed(
        FinancialStatementType StatementType,
        StatementLineSeed[] Lines,
        StatementMappingSeed[] Mappings);

    private sealed record StatementLineSeed(
        string Code,
        string Name,
        string? ParentCode,
        int DisplayOrder,
        bool IsAssignable);

    private sealed record StatementMappingSeed(
        string AccountCode,
        string LineCode);
}
