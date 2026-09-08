using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Mappings;
using MiniErp.Application.Features.AccountStatementMappings;
using MiniErp.Application.Features.Accounts;
using MiniErp.Application.Features.FinancialStatementLines;
using MiniErp.Application.Features.JournalEntries;
using MiniErp.Domain.Entities.Accounting;
using MiniErp.Domain.Entities.CashManagement;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Persistence.Interceptors;
using MiniErp.Infrastructure.Services.AccountingSetup;
using MiniErp.Infrastructure.Services.AccountStatementMappings;
using MiniErp.Infrastructure.Services.Accounts;
using MiniErp.Infrastructure.Services.FinancialStatementLines;
using MiniErp.Infrastructure.Services.JournalEntries;
using MiniErp.Infrastructure.Services.Pagination;

namespace MiniErp.Tests.Accounting;

public sealed class AccountingSetupServiceTests
{
    static AccountingSetupServiceTests()
    {
        MappingConfiguration.Register(
            typeof(InfrastructureAssemblyMarker).Assembly);
    }

    [Fact]
    public async Task DefaultSetup_CreatesCompleteIdempotentCompanyAccountingSetup()
    {
        await using var database = await AccountingTestDatabase.CreateAsync();
        var service = database.CreateDefaultAccountingSetupService();
        await database.SetBaseCurrencyAsync(CurrencyCode.USD);

        await service.InitializeCompanyAsync(
            companyId: 1,
            effectiveDate: new DateOnly(2026, 9, 2));
        var initialCashbox = await database.GetDefaultCashboxAsync();
        var initialCustomerCollection = await database.GetCashMovementTypeAsync(
            name: "Customer Collection",
            direction: CashDirection.Receipt);

        await database.SoftDeleteDefaultCashSetupAsync();
        await service.InitializeCompanyAsync(
            companyId: 1,
            effectiveDate: new DateOnly(2026, 9, 2));

        var counts = await database.GetDefaultSetupCountsAsync();

        Assert.Equal(24, counts.Accounts);
        Assert.Equal(27, counts.AccountMappings);
        Assert.Equal(35, counts.StatementLines);
        Assert.Equal(35, counts.StatementMappings);
        Assert.Equal(1, counts.FiscalYears);
        var cashbox = await database.GetDefaultCashboxAsync();
        Assert.NotNull(cashbox);
        Assert.Equal(initialCashbox!.Id, cashbox.Id);
        Assert.Equal("CASH-MAIN", cashbox.Code);
        Assert.Equal("Main Cashbox", cashbox.Name);
        Assert.Equal(CurrencyCode.USD, cashbox.Currency);
        Assert.Equal(0m, cashbox.OpeningBalance);
        Assert.Equal(new DateOnly(2026, 9, 2), cashbox.OpeningBalanceDate);
        Assert.Equal(1m, cashbox.OpeningExchangeRate);
        Assert.Equal(0m, cashbox.BaseOpeningBalance);

        var movementTypes = await database.GetCashMovementTypesAsync();
        Assert.Equal(9, movementTypes.Count);
        Assert.Equal(initialCustomerCollection!.Id, movementTypes.Single(type =>
            type.Name == "Customer Collection" &&
            type.Direction == CashDirection.Receipt).Id);
        Assert.Equal(
            [
                "Customer Collection", "Supplier Refund", "Other Receipt",
                "Other Revenue", "Supplier Payment", "Customer Refund",
                "Driver Advance", "Other Payment", "Administrative Expense"
            ],
            movementTypes.Select(type => type.Name));
        Assert.Equal(
            "Customer Collection",
            movementTypes.Single(type => type.IsDefaultForSales).Name);
        Assert.Equal(
            "Supplier Payment",
            movementTypes.Single(type => type.IsDefaultForPurchase).Name);
        Assert.Equal(
            "Customer Refund",
            movementTypes.Single(type => type.IsDefaultForSalesReturn).Name);
        Assert.Equal(
            "Supplier Refund",
            movementTypes.Single(type => type.IsDefaultForPurchaseReturn).Name);
        Assert.All(movementTypes, type => Assert.True(type.IsActive));
        Assert.True(await database.HasDefaultAccountClassificationAsync(
            accountCode: "1200",
            statementType: FinancialStatementType.FinancialPosition,
            lineCode: "FP-120"));
        Assert.True(await database.HasDefaultAccountClassificationAsync(
            accountCode: "4100",
            statementType: FinancialStatementType.IncomeStatement,
            lineCode: "IS-110"));
        Assert.True(await database.HasDefaultAccountClassificationAsync(
            accountCode: "4100",
            statementType: FinancialStatementType.CashFlow,
            lineCode: "CF-110"));
        Assert.Equal(
            "2200",
            await database.GetMappingAccountCodeAsync(
                AccountingMappingType.EmployeeControl));
        Assert.Equal(
            "2300",
            await database.GetMappingAccountCodeAsync(
                AccountingMappingType.DriverControl));
        Assert.True(await database.HasDefaultAccountClassificationAsync(
            accountCode: "2300",
            statementType: FinancialStatementType.FinancialPosition,
            lineCode: "FP-230"));
    }

    [Fact]
    public async Task DefaultSetup_ExtendsNewFiscalYearsAndCashSourcesWithoutDuplicates()
    {
        await using var database = await AccountingTestDatabase.CreateAsync();
        var service = database.CreateDefaultAccountingSetupService();

        await service.InitializeCompanyAsync(
            companyId: 1,
            effectiveDate: new DateOnly(2026, 9, 2));
        await database.AddFutureFiscalYearAndCashSetupSourcesAsync();

        await service.EnsureFiscalYearAsync(companyId: 1, fiscalYearId: 3);
        await service.EnsureCashboxAsync(companyId: 1, cashboxId: 2);
        await service.EnsureCashMovementTypeAsync(
            companyId: 1,
            cashMovementTypeId: 10);
        await service.EnsureFiscalYearAsync(companyId: 1, fiscalYearId: 3);

        var counts = await database.GetFiscalYearSetupCountsAsync(3);

        Assert.Equal(29, counts.AccountMappings);
        Assert.Equal(35, counts.StatementLines);
        Assert.Equal(35, counts.StatementMappings);
    }

    [Fact]
    public async Task DefaultSetup_CopiesCustomAccountClassificationToNewFiscalYear()
    {
        await using var database = await AccountingTestDatabase.CreateAsync();
        var service = database.CreateDefaultAccountingSetupService();
        await service.InitializeCompanyAsync(
            companyId: 1,
            effectiveDate: new DateOnly(2026, 9, 2));
        var customAccountId = await database.AddCustomStatementMappingAsync();
        await database.AddFutureFiscalYearAndCashSetupSourcesAsync();

        await service.EnsureFiscalYearAsync(companyId: 1, fiscalYearId: 3);

        Assert.True(await database.HasStatementMappingAsync(
            fiscalYearId: 3,
            statementType: FinancialStatementType.IncomeStatement,
            accountId: customAccountId,
            lineCode: "IS-230"));
    }

    [Fact]
    public async Task NewAccounts_GenerateCompanyScopedUniqueCodes()
    {
        await using var database = await AccountingTestDatabase.CreateAsync();
        var service = database.CreateAccountService(companyId: 1);

        var first = await service.AddAsync(new AccountRequest(
            Code: null,
            Name: "حساب تلقائي أول",
            ParentAccountId: null,
            AccountType: AccountType.Asset,
            NormalBalance: NormalBalance.Debit,
            IsPosting: false));
        var second = await service.AddAsync(new AccountRequest(
            Code: "IGNORED-CODE",
            Name: "حساب تلقائي ثان",
            ParentAccountId: null,
            AccountType: AccountType.Asset,
            NormalBalance: NormalBalance.Debit,
            IsPosting: false));
        var child = await service.AddAsync(new AccountRequest(
            Code: null,
            Name: "ابن تلقائي",
            ParentAccountId: first.Value.Id,
            AccountType: AccountType.Asset,
            NormalBalance: NormalBalance.Debit,
            IsPosting: true));
        var child2 = await service.AddAsync(new AccountRequest(
            Code: null,
            Name: "ابن تلقائي ثان",
            ParentAccountId: second.Value.Id,
            AccountType: AccountType.Asset,
            NormalBalance: NormalBalance.Debit,
            IsPosting: false));
        var child3 = await service.AddAsync(new AccountRequest(
            Code: null,
            Name: "ابن تلقائي ثالث",
            ParentAccountId: second.Value.Id,
            AccountType: AccountType.Asset,
            NormalBalance: NormalBalance.Debit,
            IsPosting: true));
        var grandchild = await service.AddAsync(new AccountRequest(
            Code: null,
            Name: "حفيد تلقائي",
            ParentAccountId: child2.Value.Id,
            AccountType: AccountType.Asset,
            NormalBalance: NormalBalance.Debit,
            IsPosting: true));

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal("1000", first.Value.Code);
        Assert.Equal("2000", second.Value.Code);
        Assert.Equal("1100", child.Value.Code);
        Assert.Equal("2100", child2.Value.Code);
        Assert.Equal("2200", child3.Value.Code);
        Assert.Equal("2110", grandchild.Value.Code);
    }

    [Fact]
    public async Task JournalSelect_GroupsEveryActivePartyUnderItsControlAccount()
    {
        await using var database = await AccountingTestDatabase.CreateAsync();
        var accountService = database.CreateAccountService(companyId: 1);
        await database.AddJournalPartyFixturesAsync();
        await database.AddSecondCashboxAsync();

        var result = await accountService.GetJournalSelectAsync(fiscalYearId: 1);

        Assert.True(result.IsSuccess);
        Assert.Equal([2, 11, 7, 4, 5, 6], result.Value.Select(account => account.Id));
        var customer = Assert.Single(result.Value.Single(account => account.Id == 2).PartyGroups);
        var supplier = Assert.Single(result.Value.Single(account => account.Id == 4).PartyGroups);
        var employee = Assert.Single(result.Value.Single(account => account.Id == 5).PartyGroups);
        var driver = Assert.Single(result.Value.Single(account => account.Id == 6).PartyGroups);
        var cashbox = Assert.Single(result.Value.Single(account => account.Id == 11).PartyGroups);
        Assert.Equal(JournalPartyType.Customer, customer.PartyType);
        Assert.Equal(JournalPartyType.Supplier, supplier.PartyType);
        Assert.Equal(JournalPartyType.Employee, employee.PartyType);
        Assert.Equal(JournalPartyType.Driver, driver.PartyType);
        Assert.Equal(JournalPartyType.Cashbox, cashbox.PartyType);
        Assert.Equal(1, Assert.Single(customer.Parties).Id);
        Assert.Equal(1, Assert.Single(supplier.Parties).Id);
        Assert.Equal(1, Assert.Single(employee.Parties).Id);
        Assert.Equal(1, Assert.Single(driver.Parties).Id);
        Assert.Equal(1, Assert.Single(cashbox.Parties).Id);
        Assert.DoesNotContain(cashbox.Parties, party => party.Id == 2);
        Assert.Empty(result.Value.Single(account => account.Id == 7).PartyGroups);
    }

    [Theory]
    [InlineData(2, JournalPartyType.Customer, "BP-1")]
    [InlineData(4, JournalPartyType.Supplier, "BP-1")]
    [InlineData(5, JournalPartyType.Employee, "EMP-1")]
    [InlineData(6, JournalPartyType.Driver, "DRV-1")]
    [InlineData(11, JournalPartyType.Cashbox, "CB-1")]
    public async Task JournalEntryAdd_AllowsMappedPostingChildAccount(
        int accountId,
        JournalPartyType partyType,
        string partyCode)
    {
        await using var database = await AccountingTestDatabase.CreateAsync();
        var journalEntryService = database.CreateJournalEntryService(companyId: 1);
        await database.AddJournalPartyFixturesAsync();

        var result = await journalEntryService.AddAsync(new JournalEntryRequest(
            FiscalYearId: 1,
            EntryDate: new DateOnly(2026, 9, 8),
            Description: "قيد يدوي على حساب مرتبط",
            EntryType: JournalEntryType.Manual,
            Lines:
            [
                new JournalEntryLineRequest(
                    AccountId: accountId,
                    Description: "مدين مرتبط تشغيليًا",
                    Debit: 100m,
                    Credit: 0m,
                    PartyType: partyType,
                    PartyId: 1),
                new JournalEntryLineRequest(
                    AccountId: 7,
                    Description: "دائن",
                    Debit: 0m,
                    Credit: 100m)
            ]));

        Assert.True(result.IsSuccess);
        Assert.Equal(JournalEntryType.Manual, result.Value.EntryType);
        Assert.Equal(100m, result.Value.TotalDebit);
        Assert.Equal(100m, result.Value.TotalCredit);
        Assert.Contains(
            result.Value.Lines,
            line => line.AccountId == accountId &&
                line.PartyType == partyType &&
                line.PartyId == 1 &&
                line.PartyCode == partyCode &&
                !string.IsNullOrWhiteSpace(line.PartyName));
    }

    [Fact]
    public async Task JournalEntryAdd_RequiresMatchingActivePartyForControlAccount()
    {
        await using var database = await AccountingTestDatabase.CreateAsync();
        var service = database.CreateJournalEntryService(companyId: 1);
        await database.AddJournalPartyFixturesAsync();

        async Task<string> AddAndGetErrorAsync(
            JournalPartyType? partyType,
            int? partyId)
        {
            var result = await service.AddAsync(new JournalEntryRequest(
                FiscalYearId: 1,
                EntryDate: new DateOnly(2026, 9, 8),
                Description: "اختبار طرف إلزامي",
                EntryType: JournalEntryType.Manual,
                Lines:
                [
                    new JournalEntryLineRequest(
                        AccountId: 2,
                        Description: null,
                        Debit: 10m,
                        Credit: 0m,
                        PartyType: partyType,
                        PartyId: partyId),
                    new JournalEntryLineRequest(
                        AccountId: 7,
                        Description: null,
                        Debit: 0m,
                        Credit: 10m)
                ]));
            return Assert.Single(result.Errors).Code;
        }

        Assert.Equal(
            "JournalEntries.PartyRequired",
            await AddAndGetErrorAsync(null, null));
        Assert.Equal(
            "JournalEntries.PartyTypeNotAllowed",
            await AddAndGetErrorAsync(JournalPartyType.Supplier, 1));
        Assert.Equal(
            "JournalEntries.PartyInactive",
            await AddAndGetErrorAsync(JournalPartyType.Customer, 2));
        Assert.Equal(
            "JournalEntries.PartyNotFound",
            await AddAndGetErrorAsync(JournalPartyType.Customer, 3));

        var ordinaryAccount = await service.AddAsync(new JournalEntryRequest(
            FiscalYearId: 1,
            EntryDate: new DateOnly(2026, 9, 8),
            Description: "طرف على حساب عادي",
            EntryType: JournalEntryType.Manual,
            Lines:
            [
                new JournalEntryLineRequest(
                    AccountId: 7,
                    Description: null,
                    Debit: 10m,
                    Credit: 0m,
                    PartyType: JournalPartyType.Customer,
                    PartyId: 1),
                new JournalEntryLineRequest(
                    AccountId: 7,
                    Description: null,
                    Debit: 0m,
                    Credit: 10m)
            ]));
        Assert.Equal(
            "JournalEntries.PartyNotAllowed",
            Assert.Single(ordinaryAccount.Errors).Code);
    }

    [Fact]
    public async Task JournalEntryAdd_RequiresCashboxMappedToTheSelectedAccount()
    {
        await using var database = await AccountingTestDatabase.CreateAsync();
        var service = database.CreateJournalEntryService(companyId: 1);
        await database.AddJournalPartyFixturesAsync();
        await database.AddCashboxValidationFixturesAsync();

        async Task<string?> AddAsync(int? cashboxId)
        {
            var result = await service.AddAsync(new JournalEntryRequest(
                FiscalYearId: 1,
                EntryDate: new DateOnly(2026, 9, 8),
                Description: "اختبار خزينة مرتبطة",
                EntryType: JournalEntryType.Manual,
                Lines:
                [
                    new JournalEntryLineRequest(
                        AccountId: 11,
                        Description: null,
                        Debit: 10m,
                        Credit: 0m,
                        PartyType: cashboxId.HasValue
                            ? JournalPartyType.Cashbox
                            : null,
                        PartyId: cashboxId),
                    new JournalEntryLineRequest(
                        AccountId: 7,
                        Description: null,
                        Debit: 0m,
                        Credit: 10m,
                        PartyType: JournalPartyType.Cashbox,
                        PartyId: 2)
                ]));
            return result.IsSuccess ? null : Assert.Single(result.Errors).Code;
        }

        Assert.Equal(
            "JournalEntries.PartyNotAllowed",
            await AddAsync(2));
        Assert.Equal(
            "JournalEntries.PartyInactive",
            await AddAsync(3));
        Assert.Equal(
            "JournalEntries.PartyNotFound",
            await AddAsync(4));
        Assert.Equal(
            "JournalEntries.PartyRequired",
            await AddAsync(null));
        Assert.Null(await AddAsync(1));
    }

    [Fact]
    public async Task NewAccount_ReusesCodeOfSoftDeletedAccount()
    {
        await using var database = await AccountingTestDatabase.CreateAsync();
        var service = database.CreateAccountService(companyId: 1);

        var deletedAccount = await service.AddAsync(new AccountRequest(
            Code: null,
            Name: "حساب سيُحذف",
            ParentAccountId: null,
            AccountType: AccountType.Asset,
            NormalBalance: NormalBalance.Debit,
            IsPosting: false));
        await database.SoftDeleteAccountAsync(deletedAccount.Value.Id);
        database.ClearTracking();
        var replacementAccount = await service.AddAsync(new AccountRequest(
            Code: null,
            Name: "حساب بديل",
            ParentAccountId: null,
            AccountType: AccountType.Asset,
            NormalBalance: NormalBalance.Debit,
            IsPosting: false));

        Assert.True(replacementAccount.IsSuccess);
        Assert.Equal(deletedAccount.Value.Code, replacementAccount.Value.Code);
        Assert.Equal("1000", replacementAccount.Value.Code);
    }

    [Fact]
    public async Task Accounts_InheritParentClassificationAndPreventHierarchyCycles()
    {
        await using var database = await AccountingTestDatabase.CreateAsync();
        var service = database.CreateAccountService(companyId: 1);

        var root = await service.AddAsync(new AccountRequest(
            Code: "1000",
            Name: "الأصول",
            ParentAccountId: null,
            AccountType: AccountType.Asset,
            NormalBalance: NormalBalance.Debit,
            IsPosting: false));
        var child = await service.AddAsync(new AccountRequest(
            Code: "1100",
            Name: "الأصول المتداولة",
            ParentAccountId: root.Value.Id,
            AccountType: AccountType.Asset,
            NormalBalance: NormalBalance.Debit,
            IsPosting: false));
        var inheritedClassification = await service.AddAsync(new AccountRequest(
            Code: "5100",
            Name: "حساب فرعي",
            ParentAccountId: root.Value.Id,
            AccountType: AccountType.Expense,
            NormalBalance: NormalBalance.Credit,
            IsPosting: true));
        var updatedChild = await service.UpdateAsync(
            child.Value.Id,
            new AccountUpdateRequest(
                Code: child.Value.Code,
                Name: child.Value.Name,
                ParentAccountId: root.Value.Id,
                AccountType: AccountType.Expense,
                NormalBalance: NormalBalance.Credit,
                IsPosting: child.Value.IsPosting,
                IsActive: child.Value.IsActive,
                RowVersion: child.Value.RowVersion));
        var cycle = await service.UpdateAsync(
            root.Value.Id,
            new AccountUpdateRequest(
                Code: root.Value.Code,
                Name: root.Value.Name,
                ParentAccountId: child.Value.Id,
                AccountType: root.Value.AccountType,
                NormalBalance: root.Value.NormalBalance,
                IsPosting: root.Value.IsPosting,
                IsActive: root.Value.IsActive,
                RowVersion: root.Value.RowVersion));

        Assert.True(root.IsSuccess);
        Assert.True(child.IsSuccess);
        Assert.True(inheritedClassification.IsSuccess);
        Assert.Equal(AccountType.Asset, inheritedClassification.Value.AccountType);
        Assert.Equal(NormalBalance.Debit, inheritedClassification.Value.NormalBalance);
        Assert.True(updatedChild.IsSuccess);
        Assert.Equal(AccountType.Asset, updatedChild.Value.AccountType);
        Assert.Equal(NormalBalance.Debit, updatedChild.Value.NormalBalance);
        Assert.Equal("Accounts.HierarchyCycle", cycle.Error.Code);
    }

    [Fact]
    public async Task AccountWithMovements_CannotReceiveChildAccount()
    {
        await using var database = await AccountingTestDatabase.CreateAsync();
        var service = database.CreateAccountService(companyId: 1);

        var parent = await service.AddAsync(new AccountRequest(
            Code: "1200",
            Name: "حساب عليه حركة",
            ParentAccountId: null,
            AccountType: AccountType.Asset,
            NormalBalance: NormalBalance.Debit,
            IsPosting: false));

        await database.AddCashVoucherMovementAsync(parent.Value.Id);
        database.ClearTracking();

        var result = await service.AddAsync(new AccountRequest(
            Code: "1210",
            Name: "حساب فرعي جديد",
            ParentAccountId: parent.Value.Id,
            AccountType: AccountType.Asset,
            NormalBalance: NormalBalance.Debit,
            IsPosting: true));

        Assert.True(result.IsFailure);
        Assert.Equal("Accounts.ParentHasMovements", result.Error.Code);
    }

    [Fact]
    public async Task MappingReplace_ReturnsAllRowErrorsAndSavesAtomically()
    {
        await using var database = await AccountingTestDatabase.CreateAsync();
        var accountService = database.CreateAccountService(companyId: 1);
        var lineService = database.CreateLineService(companyId: 1);
        var mappingService = database.CreateMappingService(companyId: 1);

        var account = await accountService.AddAsync(new AccountRequest(
            Code: "4100",
            Name: "إيرادات المبيعات",
            ParentAccountId: null,
            AccountType: AccountType.Revenue,
            NormalBalance: NormalBalance.Credit,
            IsPosting: true));
        var line = await lineService.AddAsync(new FinancialStatementLineRequest(
            FiscalYearId: 1,
            StatementType: FinancialStatementType.IncomeStatement,
            Code: "IS-110",
            Name: "إيرادات المبيعات",
            ParentLineId: null,
            DisplayOrder: 110,
            IsAssignable: true));

        var invalid = await mappingService.ReplaceAsync(
            fiscalYearId: 1,
            statementType: FinancialStatementType.IncomeStatement,
            request: new ReplaceAccountStatementMappingsRequest(
                Mappings:
                [
                    new AccountStatementMappingRowRequest(
                        AccountId: account.Value.Id,
                        FinancialStatementLineId: line.Value.Id),
                    new AccountStatementMappingRowRequest(
                        AccountId: account.Value.Id,
                        FinancialStatementLineId: line.Value.Id),
                    new AccountStatementMappingRowRequest(
                        AccountId: 999,
                        FinancialStatementLineId: 999)
                ]));

        Assert.True(invalid.IsFailure);
        Assert.Contains(
            invalid.Errors,
            error => error.Code == "AccountStatementMappings.DuplicateAccount");
        Assert.Contains(
            invalid.Errors,
            error => error.Code == "AccountStatementMappings.AccountNotFound");
        Assert.Contains(
            invalid.Errors,
            error => error.Code == "AccountStatementMappings.LineNotFound");
        Assert.Empty((await mappingService.GetAsync(
            1,
            FinancialStatementType.IncomeStatement)).Value);

        var saved = await mappingService.ReplaceAsync(
            fiscalYearId: 1,
            statementType: FinancialStatementType.IncomeStatement,
            request: new ReplaceAccountStatementMappingsRequest(
                Mappings:
                [
                    new AccountStatementMappingRowRequest(
                        AccountId: account.Value.Id,
                        FinancialStatementLineId: line.Value.Id)
                ]));

        Assert.True(saved.IsSuccess);
        Assert.Single(saved.Value);
        Assert.Equal(account.Value.Id, saved.Value[0].AccountId);
        Assert.Equal(line.Value.Id, saved.Value[0].FinancialStatementLineId);
    }

    [Fact]
    public async Task ClosedFiscalYear_BlocksLineAndMappingChangesButAllowsReading()
    {
        await using var database = await AccountingTestDatabase.CreateAsync();
        var accountService = database.CreateAccountService(companyId: 1);
        var lineService = database.CreateLineService(companyId: 1);
        var mappingService = database.CreateMappingService(companyId: 1);

        var account = await accountService.AddAsync(new AccountRequest(
            Code: "1200",
            Name: "العملاء",
            ParentAccountId: null,
            AccountType: AccountType.Asset,
            NormalBalance: NormalBalance.Debit,
            IsPosting: true));
        var line = await lineService.AddAsync(new FinancialStatementLineRequest(
            FiscalYearId: 1,
            StatementType: FinancialStatementType.FinancialPosition,
            Code: "FP-120",
            Name: "العملاء",
            ParentLineId: null,
            DisplayOrder: 120,
            IsAssignable: true));
        var saved = await mappingService.ReplaceAsync(
            fiscalYearId: 1,
            statementType: FinancialStatementType.FinancialPosition,
            request: new ReplaceAccountStatementMappingsRequest(
                Mappings:
                [
                    new AccountStatementMappingRowRequest(
                        AccountId: account.Value.Id,
                        FinancialStatementLineId: line.Value.Id)
                ]));

        await database.CloseFiscalYearAsync(1);
        database.ClearTracking();

        var addLine = await lineService.AddAsync(new FinancialStatementLineRequest(
            FiscalYearId: 1,
            StatementType: FinancialStatementType.FinancialPosition,
            Code: "FP-130",
            Name: "المخزون",
            ParentLineId: null,
            DisplayOrder: 130,
            IsAssignable: true));
        var replace = await mappingService.ReplaceAsync(
            fiscalYearId: 1,
            statementType: FinancialStatementType.FinancialPosition,
            request: new ReplaceAccountStatementMappingsRequest(Mappings: []));
        var read = await mappingService.GetAsync(
            1,
            FinancialStatementType.FinancialPosition);

        Assert.True(saved.IsSuccess);
        Assert.Equal("FinancialStatementLines.FiscalYearClosed", addLine.Error.Code);
        Assert.Equal("AccountStatementMappings.FiscalYearClosed", replace.Error.Code);
        Assert.True(read.IsSuccess);
        Assert.Single(read.Value);
    }

    private sealed class AccountingTestDatabase : IAsyncDisposable
    {
        private AccountingTestDatabase(
            SqliteConnection connection,
            ApplicationDbContext context)
        {
            Connection = connection;
            Context = context;
        }

        private SqliteConnection Connection { get; }

        private ApplicationDbContext Context { get; }

        public static async Task<AccountingTestDatabase> CreateAsync()
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
            await CreateSchemaAsync(context);
            return new AccountingTestDatabase(connection, context);
        }

        public AccountService CreateAccountService(int companyId) =>
            new(
                Context,
                new PaginationService(),
                new TestCurrentCompanyContext(companyId));

        public JournalEntryService CreateJournalEntryService(int companyId) =>
            new(
                Context,
                new TestCurrentCompanyContext(companyId),
                TimeProvider.System);

        public FinancialStatementLineService CreateLineService(int companyId) =>
            new(
                Context,
                new PaginationService(),
                new TestCurrentCompanyContext(companyId));

        public AccountStatementMappingService CreateMappingService(int companyId) =>
            new(Context, new TestCurrentCompanyContext(companyId));

        public DefaultAccountingSetupService
            CreateDefaultAccountingSetupService() => new(Context);

        public Task AddCashSetupSourcesAsync() =>
            Context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO Cashboxes (
                    Id, CompanyId, Code, Name, Currency, OpeningBalance,
                    OpeningBalanceDate, IsActive, IsDeleted)
                VALUES (
                    1, 1, 'CASH-MAIN', 'Main Cashbox', 1, 0,
                    '2026-09-02', 1, 0);

                INSERT INTO CashMovementTypes (
                    Id, CompanyId, Name, Direction, Classification,
                    PartnerEffect, IsActive, IsDeleted)
                VALUES (
                    1, 1, 'Customer Collection', 1, 1, 2, 1, 0);
                """);

        public Task<Cashbox?> GetDefaultCashboxAsync() =>
            Context.Cashboxes
                .AsNoTracking()
                .SingleOrDefaultAsync(cashbox =>
                    cashbox.CompanyId == 1 &&
                    cashbox.Code == "CASH-MAIN");

        public Task SetBaseCurrencyAsync(CurrencyCode currency) =>
            Context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE CompanySettings SET BaseCurrency = {(int)currency} WHERE CompanyId = 1");

        public Task<CashMovementType?> GetCashMovementTypeAsync(
            string name,
            CashDirection direction) =>
            Context.CashMovementTypes
                .AsNoTracking()
                .SingleOrDefaultAsync(movementType =>
                    movementType.CompanyId == 1 &&
                    movementType.Name == name &&
                    movementType.Direction == direction);

        public Task<List<CashMovementType>> GetCashMovementTypesAsync() =>
            Context.CashMovementTypes
                .AsNoTracking()
                .Where(movementType => movementType.CompanyId == 1)
                .OrderBy(movementType => movementType.Id)
                .ToListAsync();

        public async Task SoftDeleteDefaultCashSetupAsync()
        {
            await Context.Database.ExecuteSqlRawAsync(
                """
                UPDATE Cashboxes
                SET IsDeleted = 1
                WHERE CompanyId = 1 AND Code = 'CASH-MAIN';
                UPDATE CashMovementTypes
                SET IsDeleted = 1
                WHERE CompanyId = 1 AND Name = 'Customer Collection';
                """);
            Context.ChangeTracker.Clear();
        }

        public Task AddFutureFiscalYearAndCashSetupSourcesAsync() =>
            Context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO FiscalYears (
                    Id, CompanyId, Name, StartDate, EndDate, Status, IsCurrent,
                    CreatedById, CreatedOn, CreatedByPc, IsDeleted)
                VALUES (
                    3, 1, '2027', '2027-01-01', '2027-12-31', 1, 0,
                    'test', CURRENT_TIMESTAMP, 'test', 0);

                INSERT INTO Cashboxes (
                    Id, CompanyId, Code, Name, Currency, OpeningBalance,
                    OpeningBalanceDate, IsActive, IsDeleted)
                VALUES (
                    2, 1, 'CB-2', 'خزينة سنة لاحقة', 1, 0,
                    '2027-01-01', 1, 0);

                INSERT INTO CashMovementTypes (
                    Id, CompanyId, Name, Direction, Classification,
                    PartnerEffect, IsActive, IsDeleted)
                VALUES (
                    10, 1, 'حركة سنة لاحقة', 2, 2, 0, 1, 0);
                """);

        public async Task<int> AddCustomStatementMappingAsync()
        {
            var account = new Account
            {
                CompanyId = 1,
                Code = "5600",
                Name = "مصروف مخصص",
                AccountType = AccountType.Expense,
                NormalBalance = NormalBalance.Debit,
                IsPosting = true,
                IsActive = true
            };
            Context.Accounts.Add(account);
            await Context.SaveChangesAsync();
            var lineId = await Context.FinancialStatementLines
                .Where(line =>
                    line.CompanyId == 1 &&
                    line.FiscalYearId == 1 &&
                    line.StatementType == FinancialStatementType.IncomeStatement &&
                    line.Code == "IS-230")
                .Select(line => line.Id)
                .SingleAsync();
            Context.AccountStatementMappings.Add(new AccountStatementMapping
            {
                CompanyId = 1,
                FiscalYearId = 1,
                StatementType = FinancialStatementType.IncomeStatement,
                AccountId = account.Id,
                FinancialStatementLineId = lineId
            });
            await Context.SaveChangesAsync();
            return account.Id;
        }

        public Task<bool> HasStatementMappingAsync(
            int fiscalYearId,
            FinancialStatementType statementType,
            int accountId,
            string lineCode) =>
            Context.AccountStatementMappings.AnyAsync(mapping =>
                mapping.CompanyId == 1 &&
                mapping.FiscalYearId == fiscalYearId &&
                mapping.StatementType == statementType &&
                mapping.AccountId == accountId &&
                mapping.FinancialStatementLine.Code == lineCode);

        public Task<bool> HasDefaultAccountClassificationAsync(
            string accountCode,
            FinancialStatementType statementType,
            string lineCode) =>
            Context.AccountStatementMappings.AnyAsync(mapping =>
                mapping.CompanyId == 1 &&
                mapping.FiscalYearId == 1 &&
                mapping.StatementType == statementType &&
                mapping.Account.Code == accountCode &&
                mapping.FinancialStatementLine.Code == lineCode);

        public Task<string> GetMappingAccountCodeAsync(
            AccountingMappingType mappingType) =>
            Context.AccountMappings
                .Where(mapping =>
                    mapping.CompanyId == 1 &&
                    mapping.FiscalYearId == 1 &&
                    mapping.MappingType == mappingType &&
                    mapping.SourceId == null)
                .Select(mapping => mapping.Account.Code)
                .SingleAsync();

        public async Task<(int Accounts, int AccountMappings,
            int StatementLines, int StatementMappings, int FiscalYears)>
            GetDefaultSetupCountsAsync() =>
            (
                await Context.Accounts.CountAsync(
                    account => account.CompanyId == 1),
                await Context.AccountMappings.CountAsync(
                    mapping => mapping.CompanyId == 1),
                await Context.FinancialStatementLines.CountAsync(
                    line => line.CompanyId == 1),
                await Context.AccountStatementMappings.CountAsync(
                    mapping => mapping.CompanyId == 1),
                await Context.FiscalYears.CountAsync(
                    year => year.CompanyId == 1)
            );

        public async Task<(int AccountMappings, int StatementLines,
            int StatementMappings)> GetFiscalYearSetupCountsAsync(
            int fiscalYearId) =>
            (
                await Context.AccountMappings.CountAsync(mapping =>
                    mapping.CompanyId == 1 &&
                    mapping.FiscalYearId == fiscalYearId),
                await Context.FinancialStatementLines.CountAsync(line =>
                    line.CompanyId == 1 &&
                    line.FiscalYearId == fiscalYearId),
                await Context.AccountStatementMappings.CountAsync(mapping =>
                    mapping.CompanyId == 1 &&
                    mapping.FiscalYearId == fiscalYearId)
            );

        public Task CloseFiscalYearAsync(int fiscalYearId) =>
            Context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE FiscalYears SET Status = 2 WHERE Id = {fiscalYearId}");

        public void ClearTracking() => Context.ChangeTracker.Clear();

        public Task AddCashVoucherMovementAsync(int accountId) =>
            Context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO CashVouchers (CompanyId, AccountId, IsDeleted) VALUES (1, {accountId}, 0)");

        public Task AddSecondCashboxAsync() =>
            Context.Database.ExecuteSqlRawAsync(
                "INSERT INTO Cashboxes (Id, CompanyId, Code, Name, IsActive, IsDeleted) " +
                "VALUES (2, 1, 'CB-2', 'خزينة ثانية', 1, 0)");

        public Task AddCashboxValidationFixturesAsync() =>
            Context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO Cashboxes (Id, CompanyId, Code, Name, IsActive, IsDeleted)
                VALUES
                    (2, 1, 'CB-2', 'خزينة مربوطة بحساب آخر', 1, 0),
                    (3, 1, 'CB-3', 'خزينة غير فعالة', 0, 0),
                    (4, 2, 'CB-4', 'خزينة شركة أخرى', 1, 0);
                INSERT INTO AccountMappings (
                    CompanyId, FiscalYearId, MappingType, SourceId, AccountId,
                    CreatedById, CreatedOn, CreatedByPc, IsDeleted)
                VALUES
                    (1, 1, 1, 2, 7, 'test', CURRENT_TIMESTAMP, 'test', 0),
                    (1, 1, 1, 3, 11, 'test', CURRENT_TIMESTAMP, 'test', 0),
                    (1, 1, 1, 4, 11, 'test', CURRENT_TIMESTAMP, 'test', 0);
                """);

        public Task AddJournalPartyFixturesAsync() =>
            Context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO Accounts (
                    Id, CompanyId, Code, Name, ParentAccountId, AccountType,
                    NormalBalance, IsPosting, IsActive,
                    CreatedById, CreatedOn, CreatedByPc, IsDeleted)
                VALUES
                    (1, 1, '1000', 'الأصول', NULL, 1, 1, 0, 1,
                     'test', CURRENT_TIMESTAMP, 'test', 0),
                    (2, 1, '1200', 'العملاء', 1, 1, 1, 1, 1,
                     'test', CURRENT_TIMESTAMP, 'test', 0),
                    (3, 1, '2000', 'الالتزامات', NULL, 2, 2, 0, 1,
                     'test', CURRENT_TIMESTAMP, 'test', 0),
                    (4, 1, '2100', 'الموردون', 3, 2, 2, 1, 1,
                     'test', CURRENT_TIMESTAMP, 'test', 0),
                    (5, 1, '2200', 'مستحقات الموظفين', 3, 2, 2, 1, 1,
                     'test', CURRENT_TIMESTAMP, 'test', 0),
                    (6, 1, '2300', 'مستحقات السائقين', 3, 2, 2, 1, 1,
                     'test', CURRENT_TIMESTAMP, 'test', 0),
                    (7, 1, '1500', 'حساب عادي', 1, 1, 1, 1, 1,
                     'test', CURRENT_TIMESTAMP, 'test', 0),
                    (11, 1, '1400', 'حساب خزائن', 1, 1, 1, 1, 1,
                     'test', CURRENT_TIMESTAMP, 'test', 0),
                    (8, 1, '1600', 'حساب غير فعال', 1, 1, 1, 1, 0,
                     'test', CURRENT_TIMESTAMP, 'test', 0),
                    (9, 2, '1000', 'أصل شركة أخرى', NULL, 1, 1, 0, 1,
                     'test', CURRENT_TIMESTAMP, 'test', 0),
                    (10, 2, '1100', 'حساب شركة أخرى', 9, 1, 1, 1, 1,
                     'test', CURRENT_TIMESTAMP, 'test', 0);

                INSERT INTO AccountMappings (
                    CompanyId, FiscalYearId, MappingType, SourceId, AccountId,
                    CreatedById, CreatedOn, CreatedByPc, IsDeleted)
                VALUES
                    (1, 1, 9, NULL, 2, 'test', CURRENT_TIMESTAMP, 'test', 0),
                    (1, 1, 10, NULL, 4, 'test', CURRENT_TIMESTAMP, 'test', 0),
                    (1, 1, 11, NULL, 5, 'test', CURRENT_TIMESTAMP, 'test', 0),
                    (1, 1, 12, NULL, 6, 'test', CURRENT_TIMESTAMP, 'test', 0),
                    (1, 1, 1, 1, 11, 'test', CURRENT_TIMESTAMP, 'test', 0);

                INSERT INTO BusinessPartners (
                    Id, CompanyId, Code, Name, IsActive, IsDeleted)
                VALUES
                    (1, 1, 'BP-1', 'طرف مشترك', 1, 0),
                    (2, 1, 'BP-2', 'طرف غير فعال', 0, 0),
                    (3, 2, 'BP-3', 'طرف شركة أخرى', 1, 0);

                INSERT INTO Employees (
                    Id, CompanyId, Code, Name, IsActive, IsDeleted)
                VALUES
                    (1, 1, 'EMP-1', 'موظف فعال', 1, 0),
                    (2, 1, 'EMP-2', 'موظف غير فعال', 0, 0),
                    (3, 2, 'EMP-3', 'موظف شركة أخرى', 1, 0);

                INSERT INTO Drivers (
                    Id, CompanyId, Code, Name, IsActive, IsDeleted)
                VALUES
                    (1, 1, 'DRV-1', 'سائق فعال', 1, 0),
                    (2, 1, 'DRV-2', 'سائق غير فعال', 0, 0),
                    (3, 2, 'DRV-3', 'سائق شركة أخرى', 1, 0);

                INSERT INTO Cashboxes (Id, CompanyId, Code, Name, IsActive, IsDeleted)
                VALUES (1, 1, 'CB-1', 'خزينة رئيسية', 1, 0);
                """);

        public Task SoftDeleteAccountAsync(int accountId) =>
            Context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE Accounts SET IsDeleted = 1 WHERE Id = {accountId}");

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await Connection.DisposeAsync();
        }

        private static Task CreateSchemaAsync(ApplicationDbContext context) =>
            context.Database.ExecuteSqlRawAsync(
                """
                PRAGMA foreign_keys = ON;

                CREATE TABLE Companies (
                    Id INTEGER PRIMARY KEY,
                    Name TEXT NULL,
                    IsDeleted INTEGER NOT NULL DEFAULT 0
                );

                CREATE TABLE CompanySettings (
                    CompanyId INTEGER PRIMARY KEY,
                    BaseCurrency INTEGER NOT NULL DEFAULT 1,
                    StockBalanceCheckMode INTEGER NOT NULL DEFAULT 0,
                    FOREIGN KEY (CompanyId) REFERENCES Companies (Id)
                );

                CREATE TABLE FiscalYears (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    Name TEXT NOT NULL,
                    StartDate TEXT NOT NULL,
                    EndDate TEXT NOT NULL,
                    Status INTEGER NOT NULL,
                    IsCurrent INTEGER NOT NULL DEFAULT 0,
                    ClosedOn TEXT NULL,
                    RowVersion BLOB NOT NULL DEFAULT (randomblob(8)),
                    CreatedById TEXT NOT NULL,
                    CreatedOn TEXT NOT NULL,
                    CreatedByPc TEXT NOT NULL,
                    UpdatedById TEXT NULL,
                    UpdatedOn TEXT NULL,
                    UpdatedByPc TEXT NULL,
                    DeletedById TEXT NULL,
                    DeletedOn TEXT NULL,
                    DeletedByPc TEXT NULL,
                    IsDeleted INTEGER NOT NULL DEFAULT 0
                );

                CREATE TABLE Accounts (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    Code TEXT NOT NULL,
                    Name TEXT NOT NULL,
                    ParentAccountId INTEGER NULL,
                    AccountType INTEGER NOT NULL,
                    NormalBalance INTEGER NOT NULL,
                    IsPosting INTEGER NOT NULL DEFAULT 0,
                    IsActive INTEGER NOT NULL DEFAULT 1,
                    RowVersion BLOB NOT NULL DEFAULT (randomblob(8)),
                    CreatedById TEXT NOT NULL,
                    CreatedOn TEXT NOT NULL,
                    CreatedByPc TEXT NOT NULL,
                    UpdatedById TEXT NULL,
                    UpdatedOn TEXT NULL,
                    UpdatedByPc TEXT NULL,
                    DeletedById TEXT NULL,
                    DeletedOn TEXT NULL,
                    DeletedByPc TEXT NULL,
                    IsDeleted INTEGER NOT NULL DEFAULT 0,
                    FOREIGN KEY (CompanyId) REFERENCES Companies (Id),
                    FOREIGN KEY (ParentAccountId) REFERENCES Accounts (Id)
                );

                CREATE UNIQUE INDEX UX_Accounts_Company_Code
                ON Accounts (CompanyId, Code) WHERE IsDeleted = 0;

                CREATE TABLE BusinessPartners (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    Code TEXT NOT NULL,
                    Name TEXT NOT NULL,
                    IsActive INTEGER NOT NULL DEFAULT 1,
                    IsDeleted INTEGER NOT NULL DEFAULT 0
                );

                CREATE TABLE Employees (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    Code TEXT NOT NULL,
                    Name TEXT NOT NULL,
                    IsActive INTEGER NOT NULL DEFAULT 1,
                    IsDeleted INTEGER NOT NULL DEFAULT 0
                );

                CREATE TABLE Drivers (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    Code TEXT NOT NULL,
                    Name TEXT NOT NULL,
                    IsActive INTEGER NOT NULL DEFAULT 1,
                    IsDeleted INTEGER NOT NULL DEFAULT 0
                );

                CREATE TABLE AccountMappings (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    FiscalYearId INTEGER NOT NULL,
                    MappingType INTEGER NOT NULL,
                    SourceId INTEGER NULL,
                    AccountId INTEGER NOT NULL,
                    RowVersion BLOB NOT NULL DEFAULT (randomblob(8)),
                    CreatedById TEXT NOT NULL,
                    CreatedOn TEXT NOT NULL,
                    CreatedByPc TEXT NOT NULL,
                    UpdatedById TEXT NULL,
                    UpdatedOn TEXT NULL,
                    UpdatedByPc TEXT NULL,
                    DeletedById TEXT NULL,
                    DeletedOn TEXT NULL,
                    DeletedByPc TEXT NULL,
                    IsDeleted INTEGER NOT NULL DEFAULT 0,
                    FOREIGN KEY (CompanyId) REFERENCES Companies (Id),
                    FOREIGN KEY (FiscalYearId) REFERENCES FiscalYears (Id),
                    FOREIGN KEY (AccountId) REFERENCES Accounts (Id)
                );

                CREATE UNIQUE INDEX UX_AccountMappings_Scope_Type_Source
                ON AccountMappings (
                    CompanyId, FiscalYearId, MappingType, SourceId)
                WHERE IsDeleted = 0;

                CREATE TABLE Cashboxes (
                    Id INTEGER PRIMARY KEY,
                    CompanyId INTEGER NOT NULL,
                    Code TEXT NOT NULL DEFAULT '',
                    Name TEXT NOT NULL DEFAULT '',
                    Currency INTEGER NOT NULL DEFAULT 1,
                    OpeningBalance TEXT NOT NULL DEFAULT 0,
                    OpeningBalanceDate TEXT NOT NULL DEFAULT '0001-01-01',
                    OpeningExchangeRateId INTEGER NULL,
                    OpeningExchangeRate TEXT NOT NULL DEFAULT 1,
                    BaseOpeningBalance TEXT NOT NULL DEFAULT 0,
                    IsActive INTEGER NOT NULL DEFAULT 1,
                    Notes TEXT NULL,
                    RowVersion BLOB NOT NULL DEFAULT (randomblob(8)),
                    CreatedById TEXT NOT NULL DEFAULT '',
                    CreatedOn TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    CreatedByPc TEXT NOT NULL DEFAULT '',
                    UpdatedById TEXT NULL,
                    UpdatedOn TEXT NULL,
                    UpdatedByPc TEXT NULL,
                    DeletedById TEXT NULL,
                    DeletedOn TEXT NULL,
                    DeletedByPc TEXT NULL,
                    IsDeleted INTEGER NOT NULL DEFAULT 0,
                    FOREIGN KEY (CompanyId) REFERENCES Companies (Id)
                );

                CREATE UNIQUE INDEX UX_Cashboxes_Company_Code
                ON Cashboxes (CompanyId, Code) WHERE IsDeleted = 0;

                CREATE TABLE CashMovementTypes (
                    Id INTEGER PRIMARY KEY,
                    CompanyId INTEGER NOT NULL,
                    Name TEXT NOT NULL DEFAULT '',
                    Direction INTEGER NOT NULL,
                    Classification INTEGER NOT NULL,
                    PartnerEffect INTEGER NOT NULL DEFAULT 0,
                    IsActive INTEGER NOT NULL DEFAULT 1,
                    IsDefaultForSales INTEGER NOT NULL DEFAULT 0,
                    IsDefaultForPurchase INTEGER NOT NULL DEFAULT 0,
                    IsDefaultForSalesReturn INTEGER NOT NULL DEFAULT 0,
                    IsDefaultForPurchaseReturn INTEGER NOT NULL DEFAULT 0,
                    Notes TEXT NULL,
                    RowVersion BLOB NOT NULL DEFAULT (randomblob(8)),
                    CreatedById TEXT NOT NULL DEFAULT '',
                    CreatedOn TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    CreatedByPc TEXT NOT NULL DEFAULT '',
                    UpdatedById TEXT NULL,
                    UpdatedOn TEXT NULL,
                    UpdatedByPc TEXT NULL,
                    DeletedById TEXT NULL,
                    DeletedOn TEXT NULL,
                    DeletedByPc TEXT NULL,
                    IsDeleted INTEGER NOT NULL DEFAULT 0,
                    FOREIGN KEY (CompanyId) REFERENCES Companies (Id)
                );

                CREATE UNIQUE INDEX UX_CashMovementTypes_Company_Direction_Name
                ON CashMovementTypes (CompanyId, Direction, Name)
                WHERE IsDeleted = 0;

                CREATE UNIQUE INDEX UX_CashMovementTypes_Default_Sales
                ON CashMovementTypes (CompanyId, IsDefaultForSales)
                WHERE IsDeleted = 0 AND IsDefaultForSales = 1;

                CREATE UNIQUE INDEX UX_CashMovementTypes_Default_Purchase
                ON CashMovementTypes (CompanyId, IsDefaultForPurchase)
                WHERE IsDeleted = 0 AND IsDefaultForPurchase = 1;

                CREATE UNIQUE INDEX UX_CashMovementTypes_Default_SalesReturn
                ON CashMovementTypes (CompanyId, IsDefaultForSalesReturn)
                WHERE IsDeleted = 0 AND IsDefaultForSalesReturn = 1;

                CREATE UNIQUE INDEX UX_CashMovementTypes_Default_PurchaseReturn
                ON CashMovementTypes (CompanyId, IsDefaultForPurchaseReturn)
                WHERE IsDeleted = 0 AND IsDefaultForPurchaseReturn = 1;

                CREATE TABLE CashVouchers (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    AccountId INTEGER NULL,
                    Classification INTEGER NULL,
                    IsDeleted INTEGER NOT NULL DEFAULT 0
                );

                CREATE TABLE JournalEntries (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    FiscalYearId INTEGER NOT NULL,
                    EntryNumber TEXT NOT NULL,
                    EntryDate TEXT NOT NULL,
                    Description TEXT NOT NULL,
                    EntryType INTEGER NOT NULL,
                    SourceType INTEGER NULL,
                    SourceId INTEGER NULL,
                    SourceNumber TEXT NULL,
                    Status INTEGER NOT NULL,
                    PostedOn TEXT NOT NULL,
                    ReversedOn TEXT NULL,
                    ReversalOfEntryId INTEGER NULL,
                    RowVersion BLOB NOT NULL DEFAULT (randomblob(8)),
                    CreatedById TEXT NOT NULL,
                    CreatedOn TEXT NOT NULL,
                    CreatedByPc TEXT NOT NULL,
                    UpdatedById TEXT NULL,
                    UpdatedOn TEXT NULL,
                    UpdatedByPc TEXT NULL,
                    DeletedById TEXT NULL,
                    DeletedOn TEXT NULL,
                    DeletedByPc TEXT NULL,
                    IsDeleted INTEGER NOT NULL DEFAULT 0,
                    FOREIGN KEY (CompanyId) REFERENCES Companies (Id),
                    FOREIGN KEY (FiscalYearId) REFERENCES FiscalYears (Id)
                );

                CREATE UNIQUE INDEX UX_JournalEntries_Company_Number
                ON JournalEntries (CompanyId, EntryNumber) WHERE IsDeleted = 0;

                CREATE TABLE JournalEntryLines (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    JournalEntryId INTEGER NOT NULL,
                    AccountId INTEGER NOT NULL,
                    PartyType INTEGER NULL,
                    PartyId INTEGER NULL,
                    Description TEXT NULL,
                    Debit TEXT NOT NULL,
                    Credit TEXT NOT NULL,
                    CreatedById TEXT NOT NULL,
                    CreatedOn TEXT NOT NULL,
                    CreatedByPc TEXT NOT NULL,
                    UpdatedById TEXT NULL,
                    UpdatedOn TEXT NULL,
                    UpdatedByPc TEXT NULL,
                    DeletedById TEXT NULL,
                    DeletedOn TEXT NULL,
                    DeletedByPc TEXT NULL,
                    IsDeleted INTEGER NOT NULL DEFAULT 0,
                    FOREIGN KEY (CompanyId) REFERENCES Companies (Id),
                    FOREIGN KEY (JournalEntryId) REFERENCES JournalEntries (Id),
                    FOREIGN KEY (AccountId) REFERENCES Accounts (Id)
                );

                CREATE TABLE FinancialStatementLines (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    FiscalYearId INTEGER NOT NULL,
                    StatementType INTEGER NOT NULL,
                    Code TEXT NOT NULL,
                    Name TEXT NOT NULL,
                    ParentLineId INTEGER NULL,
                    DisplayOrder INTEGER NOT NULL,
                    IsAssignable INTEGER NOT NULL DEFAULT 0,
                    IsActive INTEGER NOT NULL DEFAULT 1,
                    RowVersion BLOB NOT NULL DEFAULT (randomblob(8)),
                    CreatedById TEXT NOT NULL,
                    CreatedOn TEXT NOT NULL,
                    CreatedByPc TEXT NOT NULL,
                    UpdatedById TEXT NULL,
                    UpdatedOn TEXT NULL,
                    UpdatedByPc TEXT NULL,
                    DeletedById TEXT NULL,
                    DeletedOn TEXT NULL,
                    DeletedByPc TEXT NULL,
                    IsDeleted INTEGER NOT NULL DEFAULT 0,
                    FOREIGN KEY (CompanyId) REFERENCES Companies (Id),
                    FOREIGN KEY (FiscalYearId) REFERENCES FiscalYears (Id),
                    FOREIGN KEY (ParentLineId) REFERENCES FinancialStatementLines (Id)
                );

                CREATE UNIQUE INDEX UX_FinancialStatementLines_Scope_Code
                ON FinancialStatementLines (CompanyId, FiscalYearId, StatementType, Code)
                WHERE IsDeleted = 0;

                CREATE TABLE AccountStatementMappings (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    FiscalYearId INTEGER NOT NULL,
                    StatementType INTEGER NOT NULL,
                    AccountId INTEGER NOT NULL,
                    FinancialStatementLineId INTEGER NOT NULL,
                    CreatedById TEXT NOT NULL,
                    CreatedOn TEXT NOT NULL,
                    CreatedByPc TEXT NOT NULL,
                    UpdatedById TEXT NULL,
                    UpdatedOn TEXT NULL,
                    UpdatedByPc TEXT NULL,
                    DeletedById TEXT NULL,
                    DeletedOn TEXT NULL,
                    DeletedByPc TEXT NULL,
                    IsDeleted INTEGER NOT NULL DEFAULT 0,
                    FOREIGN KEY (CompanyId) REFERENCES Companies (Id),
                    FOREIGN KEY (FiscalYearId) REFERENCES FiscalYears (Id),
                    FOREIGN KEY (AccountId) REFERENCES Accounts (Id),
                    FOREIGN KEY (FinancialStatementLineId) REFERENCES FinancialStatementLines (Id)
                );

                CREATE UNIQUE INDEX UX_AccountStatementMappings_Scope_Account
                ON AccountStatementMappings (CompanyId, FiscalYearId, StatementType, AccountId)
                WHERE IsDeleted = 0;

                CREATE TRIGGER AdvanceAccountRowVersion
                AFTER UPDATE ON Accounts
                BEGIN
                    UPDATE Accounts SET RowVersion = randomblob(8) WHERE Id = NEW.Id;
                END;

                CREATE TRIGGER AdvanceStatementLineRowVersion
                AFTER UPDATE ON FinancialStatementLines
                BEGIN
                    UPDATE FinancialStatementLines
                    SET RowVersion = randomblob(8) WHERE Id = NEW.Id;
                END;

                INSERT INTO Companies (Id, Name)
                VALUES (1, 'Company 1'), (2, 'Company 2');

                INSERT INTO CompanySettings (CompanyId, BaseCurrency)
                VALUES (1, 1), (2, 1);

                INSERT INTO FiscalYears (
                    Id, CompanyId, Name, StartDate, EndDate, Status, IsCurrent,
                    CreatedById, CreatedOn, CreatedByPc, IsDeleted)
                VALUES (
                    1, 1, '2026', '2026-01-01', '2026-12-31', 1, 1,
                    'test', CURRENT_TIMESTAMP, 'test', 0),
                    (2, 2, '2026', '2026-01-01', '2026-12-31', 1, 1,
                    'test', CURRENT_TIMESTAMP, 'test', 0);
                """);

        private sealed record TestCurrentCompanyContext(int CompanyId)
            : ICurrentCompanyContext;
    }
}
