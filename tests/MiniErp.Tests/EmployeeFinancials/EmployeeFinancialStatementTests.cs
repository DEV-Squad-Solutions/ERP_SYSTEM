using MiniErp.Application.Common.Models;
using MiniErp.Application.Features.CashVouchers;
using MiniErp.Application.Features.EmployeeMovements;
using MiniErp.Application.Features.EmployeeOpeningBalances;
using MiniErp.Application.Features.PayrollEntries;
using MiniErp.Application.Features.Statements;
using MiniErp.Domain.Entities.Employees;
using MiniErp.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using MiniErp.Tests.PayrollEntries;
using System;
using System.Threading.Tasks;
using Xunit;
using AttendanceEntity = MiniErp.Domain.Entities.Employees.EmployeeAttendance;

namespace MiniErp.Tests.EmployeeFinancials;

public sealed class EmployeeFinancialStatementTests
{
    [Fact]
    public async Task GetEmployeeStatementAsync_ShouldAccuratelyCalculateRunningBalance_FromOpeningBalanceSalaryAndMovements()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        await EnsureFinancialLedgerSchemaAsync(database.Context);
        var openingBalanceService = database.CreateOpeningBalanceService();
        var payrollService = database.CreatePayrollService();
        var statementService = database.CreateStatementService();

        // 1. Initial Opening Balance (Credit +5,000) on 2026-08-01
        var openingBalanceResult = await openingBalanceService.AddAsync(new EmployeeOpeningBalanceRequest(
            EmployeeId: 1,
            DocumentDate: new DateOnly(2026, 8, 1),
            Currency: CurrencyCode.EGP,
            BalanceType: EmployeeBalanceType.Credit,
            Amount: 5000m,
            Notes: "Initial balance"));
        Assert.True(openingBalanceResult.IsSuccess);

        // 2. Attendance and Salary Transfer (Net +2,000) on 2026-08-10
        for (int day = 1; day <= 10; day++)
        {
            database.Context.EmployeeAttendances.Add(new AttendanceEntity
            {
                CompanyId = 1,
                EmployeeId = 1,
                WorkDate = new DateOnly(2026, 8, day),
                Status = EmployeeAttendanceStatus.Present,
                WorkDayRatio = WorkDayRatio.FullDay
            });
        }
        await database.Context.SaveChangesAsync();

        var payrollResult = await payrollService.AddAsync(new PayrollEntryCreateRequest(
            EmployeeId: 1,
            EndDate: new DateOnly(2026, 8, 10)));
        Assert.True(payrollResult.IsSuccess);

        var salaryTransferResult = await payrollService.MoveSalaryForEmployeeAccountAsync(
            payrollResult.Value.Id,
            new PayrollEntrySalaryPaymentRequest(
                PostingDate: new DateOnly(2026, 8, 10),
                Notes: "Salary Transfer Period 1"));
        Assert.True(salaryTransferResult.IsSuccess);
        // NetSalary = 10 days * 200 = 2000

        var salaryBalance = await database.Context.EmployeeOpeningBalances
            .SingleAsync(balance => balance.PayrollEntryId == payrollResult.Value.Id);

        await InsertEmployeeVouchersAsync(database.Context);

        // 3. Movement: Debit (-500) on 2026-08-15
        var debitMovement = new EmployeeMovement
        {
            CompanyId = 1,
            EmployeeId = 1,
            Type = EmployeeMovementType.Debit,
            MovementDate = new DateOnly(2026, 8, 15),
            Currency = CurrencyCode.EGP,
            Notes = "Mid-month debit"
        };
        debitMovement.ApplyAmounts(EmployeeMovementType.Debit, 500m);
        debitMovement.ApplyExchangeRate(1m);
        database.Context.EmployeeMovements.Add(debitMovement);

        // 4. Movement: Performance Bonus (Credit +1,000) on 2026-08-20
        var bonus = new EmployeeMovement
        {
            CompanyId = 1,
            EmployeeId = 1,
            Type = EmployeeMovementType.Bonus,
            MovementDate = new DateOnly(2026, 8, 20),
            Currency = CurrencyCode.EGP,
            Notes = "Special bonus"
        };
        bonus.ApplyAmounts(EmployeeMovementType.Bonus, 1000m);
        bonus.ApplyExchangeRate(1m);
        database.Context.EmployeeMovements.Add(bonus);
        await database.Context.SaveChangesAsync();

        await SeedEmployeeLedgerAsync(
            database.Context,
            openingBalanceResult.Value.Id,
            salaryBalance.Id);

        // Act - Get complete statement
        var statementResult = await statementService.GetEmployeeStatementAsync(
            new PaginationRequest { PageNumber = 1, PageSize = 10 },
            new EmployeeStatementFilterRequest(EmployeeId: 1));

        // Assert
        Assert.True(statementResult.IsSuccess);
        var response = statementResult.Value;

        Assert.Equal(1, response.EmployeeId);
        Assert.Equal(4, response.Items.Count);

        // Item 1: Opening Balance (+5000) -> Running Balance = 5,000
        Assert.Equal(EmployeeStatementSourceType.OpeningBalance, response.Items[0].SourceType);
        Assert.Equal(0m, response.Items[0].DebitAmount);
        Assert.Equal(5000m, response.Items[0].CreditAmount);
        Assert.Equal(5000m, response.Items[0].BalanceAmount);

        // Item 2: Salary Transfer (+2000) -> Running Balance = 7,000
        Assert.Equal(EmployeeStatementSourceType.SalaryTransfer, response.Items[1].SourceType);
        Assert.Equal(0m, response.Items[1].DebitAmount);
        Assert.Equal(2000m, response.Items[1].CreditAmount);
        Assert.Equal(7000m, response.Items[1].BalanceAmount);

        // Item 3: Debit (-500) -> Running Balance = 6,500
        Assert.Equal(EmployeeStatementSourceType.Movement, response.Items[2].SourceType);
        Assert.Equal(500m, response.Items[2].DebitAmount);
        Assert.Equal(0m, response.Items[2].CreditAmount);
        Assert.Equal(6500m, response.Items[2].BalanceAmount);

        // Item 4: Bonus (+1000) -> Running Balance = 7,500
        Assert.Equal(EmployeeStatementSourceType.Movement, response.Items[3].SourceType);
        Assert.Equal(0m, response.Items[3].DebitAmount);
        Assert.Equal(1000m, response.Items[3].CreditAmount);
        Assert.Equal(7500m, response.Items[3].BalanceAmount);

        // Summary Assertions
        Assert.Equal(8000m, response.Summary.TotalCredits); // 5000 + 2000 + 1000
        Assert.Equal(500m, response.Summary.TotalDebits);    // 500
        Assert.Equal(7500m, response.Summary.ClosingBalanceAmount);

        // Act & Assert Balance Endpoint
        var balanceResult = await statementService.GetEmployeeBalanceAsync(1);
        Assert.True(balanceResult.IsSuccess);
        Assert.Equal(7500m, balanceResult.Value.BalanceAmount);
        Assert.Equal(8000m, balanceResult.Value.TotalCredits);
        Assert.Equal(500m, balanceResult.Value.TotalDebits);

        // Act & Assert Account Summary
        var summaryResult = await statementService.GetEmployeeAccountSummaryAsync(1);
        Assert.True(summaryResult.IsSuccess);
        Assert.Equal(1, summaryResult.Value.Employee.Id);
        Assert.Equal(5000m, summaryResult.Value.OpeningBalance);
        Assert.Equal(7500m, summaryResult.Value.CurrentBalance);
        Assert.Equal(8000m, summaryResult.Value.TotalCredits);
        Assert.Equal(500m, summaryResult.Value.TotalDebits);
        Assert.Equal(0m, summaryResult.Value.TotalAdvances);
        Assert.Equal(1000m, summaryResult.Value.TotalBonuses);
        Assert.Equal(2000m, summaryResult.Value.TotalSalaryMoved);
    }

    private static async Task EnsureFinancialLedgerSchemaAsync(
        MiniErp.Infrastructure.Persistence.ApplicationDbContext context) =>
        await context.Database.ExecuteSqlRawAsync(
            """
            DROP TABLE IF EXISTS JournalEntryLines;
            DROP TABLE IF EXISTS JournalEntries;

            CREATE TABLE JournalEntries (
                Id INTEGER PRIMARY KEY,
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
                IsDeleted INTEGER NOT NULL
            );

            CREATE TABLE JournalEntryLines (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CompanyId INTEGER NOT NULL,
                JournalEntryId INTEGER NOT NULL,
                AccountId INTEGER NOT NULL,
                PartyType INTEGER NULL,
                PartyId INTEGER NULL,
                Description TEXT NULL,
                Debit NUMERIC NOT NULL,
                Credit NUMERIC NOT NULL,
                Currency INTEGER NOT NULL,
                ExchangeRate NUMERIC NOT NULL,
                TransactionDebit NUMERIC NOT NULL,
                TransactionCredit NUMERIC NOT NULL,
                IsDeleted INTEGER NOT NULL
            );
            """);

    private static async Task InsertEmployeeVouchersAsync(
        MiniErp.Infrastructure.Persistence.ApplicationDbContext context)
    {
        await context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO CashVouchers (
                Id, CompanyId, VoucherNumber, VoucherDate, Direction,
                CashboxId, CashMovementTypeId, Classification, AccountId,
                PartyType, EmployeeId, BusinessPartnerId, DriverId,
                DriverTripId, ExternalPartyName, Amount, Currency,
                ExchangeRate, BaseAmount, IsPosted, ReferenceNumber,
                Description, Notes, LastModifiedAt, CreatedById, CreatedOn,
                CreatedByPc, IsDeleted)
            VALUES
                (9001, 1, 'EMP-ADVANCE', '2026-08-15', 2, 1, NULL, NULL,
                 NULL, 5, 1, NULL, NULL, NULL, NULL, 500, 1, 1, 500, 1,
                 'ADV-1', 'Cash advance', NULL, '2026-08-15', 'test',
                 '2026-08-15', 'test', 0),
                (9002, 1, 'EMP-BONUS', '2026-08-20', 1, 1, NULL, NULL,
                 NULL, 5, 1, NULL, NULL, NULL, NULL, 1000, 1, 1, 1000, 1,
                 'BONUS-1', 'Performance bonus', NULL, '2026-08-20',
                 'test', '2026-08-20', 'test', 0);
            """);
    }

    private static async Task SeedEmployeeLedgerAsync(
        MiniErp.Infrastructure.Persistence.ApplicationDbContext context,
        int openingBalanceId,
        int salaryBalanceId)
    {
        async Task Entry(
            int entryId,
            int sourceType,
            int sourceId,
            string number,
            string date,
            decimal debit,
            decimal credit) =>
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO JournalEntries (
                    Id, CompanyId, FiscalYearId, EntryNumber, EntryDate,
                    Description, EntryType, SourceType, SourceId,
                    SourceNumber, Status, PostedOn, IsDeleted)
                VALUES (
                    {entryId}, 1, 1, {number}, {date}, {number}, 4,
                    {sourceType}, {sourceId}, {number}, 1,
                    {date + "T00:00:00Z"}, 0);

                INSERT INTO JournalEntryLines (
                    CompanyId, JournalEntryId, AccountId, PartyType, PartyId,
                    Description, Debit, Credit, Currency, ExchangeRate,
                    TransactionDebit, TransactionCredit, IsDeleted)
                VALUES (
                    1, {entryId}, 1, 3, 1, {number}, {debit}, {credit},
                    1, 1, {debit}, {credit}, 0);
                """);

        await Entry(
            5101,
            (int)JournalEntrySourceType.EmployeeOpeningBalance,
            openingBalanceId,
            "EMP-OPENING",
            "2026-08-01",
            0m,
            5000m);
        await Entry(
            5102,
            (int)JournalEntrySourceType.EmployeeOpeningBalance,
            salaryBalanceId,
            "EMP-SALARY",
            "2026-08-10",
            0m,
            2000m);
        await Entry(
            5103,
            (int)JournalEntrySourceType.CashVoucher,
            9001,
            "EMP-ADVANCE",
            "2026-08-15",
            500m,
            0m);
        await Entry(
            5104,
            (int)JournalEntrySourceType.CashVoucher,
            9002,
            "EMP-BONUS",
            "2026-08-20",
            0m,
            1000m);
    }

    [Fact]
    public async Task EmployeeMovements_AllTypes_ShouldAccuratelyReflectDebitsCreditsAndRunningBalance()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var movementService = database.CreateMovementService();
        var statementService = database.CreateStatementService();

        // 1. Debit (-300)
        var debit = await movementService.AddAsync(new EmployeeMovementRequest(
            EmployeeId: 1,
            Type: EmployeeMovementType.Debit,
            Amount: 300m,
            Currency: CurrencyCode.EGP,
            MovementDate: new DateOnly(2026, 9, 1),
            Notes: "Debit movement"));
        Assert.True(debit.IsSuccess);

        // 2. Credit (+500)
        var credit = await movementService.AddAsync(new EmployeeMovementRequest(
            EmployeeId: 1,
            Type: EmployeeMovementType.Credit,
            Amount: 500m,
            Currency: CurrencyCode.EGP,
            MovementDate: new DateOnly(2026, 9, 2),
            Notes: "Credit movement"));
        Assert.True(credit.IsSuccess);

        // 3. Deduction (-150)
        var deduction = await movementService.AddAsync(new EmployeeMovementRequest(
            EmployeeId: 1,
            Type: EmployeeMovementType.Deduction,
            Amount: 150m,
            Currency: CurrencyCode.EGP,
            MovementDate: new DateOnly(2026, 9, 3),
            Notes: "Penalty deduction"));
        Assert.True(deduction.IsSuccess);

        // 4. Bonus (+250)
        var bonus = await movementService.AddAsync(new EmployeeMovementRequest(
            EmployeeId: 1,
            Type: EmployeeMovementType.Bonus,
            Amount: 250m,
            Currency: CurrencyCode.EGP,
            MovementDate: new DateOnly(2026, 9, 4),
            Notes: "Reward bonus"));
        Assert.True(bonus.IsSuccess);

        // Act
        var statementResult = await statementService.GetEmployeeStatementAsync(
            new PaginationRequest { PageNumber = 1, PageSize = 10 },
            new EmployeeStatementFilterRequest(EmployeeId: 1));

        // Assert
        Assert.True(statementResult.IsSuccess);
        var response = statementResult.Value;
        Assert.Equal(4, response.Items.Count);

        // Item 0: Debit (-300)
        Assert.Equal(EmployeeStatementSourceType.Movement, response.Items[0].SourceType);
        Assert.Equal(300m, response.Items[0].DebitAmount);
        Assert.Equal(0m, response.Items[0].CreditAmount);
        Assert.Equal(-300m, response.Items[0].RunningBalance);
        Assert.Equal(300m, response.Items[0].BalanceAmount);

        // Item 1: Credit (+500) -> Running Balance = -300 + 500 = +200
        Assert.Equal(EmployeeStatementSourceType.Movement, response.Items[1].SourceType);
        Assert.Equal(0m, response.Items[1].DebitAmount);
        Assert.Equal(500m, response.Items[1].CreditAmount);
        Assert.Equal(200m, response.Items[1].RunningBalance);
        Assert.Equal(200m, response.Items[1].BalanceAmount);

        // Item 2: Deduction (-150) -> Running Balance = 200 - 150 = +50
        Assert.Equal(EmployeeStatementSourceType.Movement, response.Items[2].SourceType);
        Assert.Equal(150m, response.Items[2].DebitAmount);
        Assert.Equal(0m, response.Items[2].CreditAmount);
        Assert.Equal(50m, response.Items[2].RunningBalance);
        Assert.Equal(50m, response.Items[2].BalanceAmount);

        // Item 3: Bonus (+250) -> Running Balance = 50 + 250 = +300
        Assert.Equal(EmployeeStatementSourceType.Movement, response.Items[3].SourceType);
        Assert.Equal(0m, response.Items[3].DebitAmount);
        Assert.Equal(250m, response.Items[3].CreditAmount);
        Assert.Equal(300m, response.Items[3].RunningBalance);
        Assert.Equal(300m, response.Items[3].BalanceAmount);

        // Totals: TotalDebits = 300 + 150 = 450, TotalCredits = 500 + 250 = 750
        Assert.Equal(450m, response.Summary.TotalDebits);
        Assert.Equal(750m, response.Summary.TotalCredits);
        Assert.Equal(300m, response.Summary.ClosingBalanceAmount);

        // Verify GetEmployeeBalanceAsync matches
        var balanceResult = await statementService.GetEmployeeBalanceAsync(1);
        Assert.True(balanceResult.IsSuccess);
        Assert.Equal(300m, balanceResult.Value.BalanceAmount);
        Assert.Equal(450m, balanceResult.Value.TotalDebits);
        Assert.Equal(750m, balanceResult.Value.TotalCredits);
    }

    [Fact]
    public async Task PayrollSalaryMovement_WhenNotMoved_ShouldNotAffectEmployeeAccount_WhenMoved_ShouldAffectAccountImmediately()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var payrollService = database.CreatePayrollService();
        var statementService = database.CreateStatementService();

        for (int day = 1; day <= 10; day++)
        {
            database.Context.EmployeeAttendances.Add(new AttendanceEntity
            {
                CompanyId = 1,
                EmployeeId = 1,
                WorkDate = new DateOnly(2026, 9, day),
                Status = EmployeeAttendanceStatus.Present,
                WorkDayRatio = WorkDayRatio.FullDay
            });
        }
        await database.Context.SaveChangesAsync();

        var payrollResult = await payrollService.AddAsync(new PayrollEntryCreateRequest(
            EmployeeId: 1,
            EndDate: new DateOnly(2026, 9, 10)));
        Assert.True(payrollResult.IsSuccess);
        Assert.False(payrollResult.Value.IsSalaryMoveToEmployeeAccount);

        // Act 1: Verify statement before salary movement
        var statementBefore = await statementService.GetEmployeeStatementAsync(
            new PaginationRequest { PageNumber = 1, PageSize = 10 },
            new EmployeeStatementFilterRequest(EmployeeId: 1));

        Assert.True(statementBefore.IsSuccess);
        Assert.Empty(statementBefore.Value.Items);
        Assert.Equal(0m, statementBefore.Value.Summary.ClosingBalanceAmount);

        // Act 2: Move salary
        var moveResult = await payrollService.MoveSalaryForEmployeeAccountAsync(
            payrollResult.Value.Id,
            new PayrollEntrySalaryPaymentRequest(
                PostingDate: new DateOnly(2026, 9, 10),
                Notes: "September salary transfer"));
        Assert.True(moveResult.IsSuccess);
        Assert.True(moveResult.Value.IsSalaryMoveToEmployeeAccount);

        // Act 3: Verify statement immediately reflects salary transfer
        var statementAfter = await statementService.GetEmployeeStatementAsync(
            new PaginationRequest { PageNumber = 1, PageSize = 10 },
            new EmployeeStatementFilterRequest(EmployeeId: 1));

        Assert.True(statementAfter.IsSuccess);
        Assert.Single(statementAfter.Value.Items);
        var item = statementAfter.Value.Items[0];
        Assert.Equal(EmployeeStatementSourceType.SalaryTransfer, item.SourceType);
        Assert.Equal(0m, item.DebitAmount);
        Assert.Equal(2000m, item.CreditAmount);
        Assert.Equal(2000m, item.BalanceAmount);
        Assert.Equal(2000m, item.RunningBalance);
        Assert.Equal("PAY-" + payrollResult.Value.Id, item.ReferenceNumber);

        // Act 4: Attempt moving again - should fail (idempotency)
        var secondMoveResult = await payrollService.MoveSalaryForEmployeeAccountAsync(
            payrollResult.Value.Id,
            new PayrollEntrySalaryPaymentRequest(
                PostingDate: new DateOnly(2026, 9, 10),
                Notes: "Duplicate salary transfer"));
        Assert.True(secondMoveResult.IsFailure);
    }

    [Fact]
    public async Task CashVoucher_EmployeeRelated_ShouldAffectEmployeeAccount_AndFilterBySourceType()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var cashVoucherService = database.CreateCashVoucherService();
        var statementService = database.CreateStatementService();

        // 1. Payment CashVoucher (advance/payout to employee -> Debit)
        var paymentResult = await cashVoucherService.AddAsync(new CashVoucherRequest(
            VoucherDate: new DateOnly(2026, 9, 5),
            Direction: CashDirection.Payment,
            CashboxId: 1,
            Amount: 600m,
            Description: "Cash advance for expenses",
            EmployeeId: 1,
            ReferenceNumber: "ADV-001",
            EmployeeMovementType: EmployeeMovementType.Debit),
            cancellationToken: default);
        Assert.True(paymentResult.IsSuccess);

        // 2. Receipt CashVoucher (repayment from employee -> Credit)
        var receiptResult = await cashVoucherService.AddAsync(new CashVoucherRequest(
            VoucherDate: new DateOnly(2026, 9, 15),
            Direction: CashDirection.Receipt,
            CashboxId: 1,
            Amount: 200m,
            Description: "Cash advance repayment",
            EmployeeId: 1,
            ReferenceNumber: "REP-001",
            EmployeeMovementType: EmployeeMovementType.Credit),
            cancellationToken: default);
        Assert.True(receiptResult.IsSuccess);

        // Act 1: Get complete statement
        var allStatementResult = await statementService.GetEmployeeStatementAsync(
            new PaginationRequest { PageNumber = 1, PageSize = 10 },
            new EmployeeStatementFilterRequest(EmployeeId: 1));

        Assert.True(allStatementResult.IsSuccess);
        Assert.Equal(2, allStatementResult.Value.Items.Count);

        // Payment item
        var payItem = allStatementResult.Value.Items[0];
        Assert.Equal(EmployeeStatementSourceType.CashVoucher, payItem.SourceType);
        Assert.Equal(600m, payItem.DebitAmount);
        Assert.Equal(0m, payItem.CreditAmount);
        Assert.Equal(-600m, payItem.RunningBalance);
        Assert.Equal(paymentResult.Value.Id, payItem.SourceId);
        Assert.Equal(paymentResult.Value.Id, payItem.CashVoucherId);
        Assert.Equal(paymentResult.Value.VoucherNumber, payItem.CashVoucherNumber);
        Assert.Equal("ADV-001", payItem.ReferenceNumber);

        // Receipt item
        var recItem = allStatementResult.Value.Items[1];
        Assert.Equal(EmployeeStatementSourceType.CashVoucher, recItem.SourceType);
        Assert.Equal(0m, recItem.DebitAmount);
        Assert.Equal(200m, recItem.CreditAmount);
        Assert.Equal(-400m, recItem.RunningBalance);
        Assert.Equal(receiptResult.Value.Id, recItem.SourceId);
        Assert.Equal(receiptResult.Value.Id, recItem.CashVoucherId);
        Assert.Equal(receiptResult.Value.VoucherNumber, recItem.CashVoucherNumber);

        // Act 2: Filter by CashVoucher SourceType
        var cashFilterResult = await statementService.GetEmployeeStatementAsync(
            new PaginationRequest { PageNumber = 1, PageSize = 10 },
            new EmployeeStatementFilterRequest(
                EmployeeId: 1,
                SourceType: EmployeeStatementSourceType.CashVoucher));

        Assert.True(cashFilterResult.IsSuccess);
        Assert.Equal(2, cashFilterResult.Value.Items.Count);

        // Act 3: Filter by Movement SourceType -> should return 0 (since both are CashVouchers)
        var movFilterResult = await statementService.GetEmployeeStatementAsync(
            new PaginationRequest { PageNumber = 1, PageSize = 10 },
            new EmployeeStatementFilterRequest(
                EmployeeId: 1,
                SourceType: EmployeeStatementSourceType.Movement));

        Assert.True(movFilterResult.IsSuccess);
        Assert.Empty(movFilterResult.Value.Items);
    }

    [Fact]
    public async Task CashVoucher_UnrelatedCompanyCashbox_ShouldNotAffectEmployeeAccount()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var cashVoucherService = database.CreateCashVoucherService();
        var statementService = database.CreateStatementService();

        // 1. Company cashbox payment without employee (general expense)
        var companyPayment = await cashVoucherService.AddAsync(new CashVoucherRequest(
            VoucherDate: new DateOnly(2026, 9, 1),
            Direction: CashDirection.Payment,
            CashboxId: 1,
            Amount: 1000m,
            Description: "Office supplies expense",
            EmployeeId: null),
            cancellationToken: default);
        Assert.True(companyPayment.IsSuccess);

        // 2. Cashbox payment for Employee 2
        var emp2Payment = await cashVoucherService.AddAsync(new CashVoucherRequest(
            VoucherDate: new DateOnly(2026, 9, 2),
            Direction: CashDirection.Payment,
            CashboxId: 1,
            Amount: 400m,
            Description: "Employee 2 advance",
            EmployeeId: 2,
            EmployeeMovementType: EmployeeMovementType.Debit),
            cancellationToken: default);
        Assert.True(emp2Payment.IsSuccess);

        // Act - Check Employee 1 statement and balance
        var statementResult = await statementService.GetEmployeeStatementAsync(
            new PaginationRequest { PageNumber = 1, PageSize = 10 },
            new EmployeeStatementFilterRequest(EmployeeId: 1));

        var balanceResult = await statementService.GetEmployeeBalanceAsync(1);

        // Assert
        Assert.True(statementResult.IsSuccess);
        Assert.Empty(statementResult.Value.Items);
        Assert.Equal(0m, statementResult.Value.Summary.ClosingBalanceAmount);

        Assert.True(balanceResult.IsSuccess);
        Assert.Equal(0m, balanceResult.Value.BalanceAmount);
        Assert.Equal(0m, balanceResult.Value.TotalCredits);
        Assert.Equal(0m, balanceResult.Value.TotalDebits);
    }

    [Fact]
    public async Task UnifiedLedger_NoDoubleCounting_AndReportAgreement()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var openingBalanceService = database.CreateOpeningBalanceService();
        var payrollService = database.CreatePayrollService();
        var cashVoucherService = database.CreateCashVoucherService();
        var movementService = database.CreateMovementService();
        var statementService = database.CreateStatementService();

        // 1. Opening Balance (+1,000 credit)
        var obResult = await openingBalanceService.AddAsync(new EmployeeOpeningBalanceRequest(
            EmployeeId: 1,
            DocumentDate: new DateOnly(2026, 9, 1),
            Currency: CurrencyCode.EGP,
            BalanceType: EmployeeBalanceType.Credit,
            Amount: 1000m,
            Notes: "Opening balance"));
        Assert.True(obResult.IsSuccess);

        // 2. Payroll moved (+2,000 credit)
        for (int day = 1; day <= 10; day++)
        {
            database.Context.EmployeeAttendances.Add(new AttendanceEntity
            {
                CompanyId = 1,
                EmployeeId = 1,
                WorkDate = new DateOnly(2026, 9, day),
                Status = EmployeeAttendanceStatus.Present,
                WorkDayRatio = WorkDayRatio.FullDay
            });
        }
        await database.Context.SaveChangesAsync();

        var payrollResult = await payrollService.AddAsync(new PayrollEntryCreateRequest(
            EmployeeId: 1,
            EndDate: new DateOnly(2026, 9, 10)));
        Assert.True(payrollResult.IsSuccess);

        var moveResult = await payrollService.MoveSalaryForEmployeeAccountAsync(
            payrollResult.Value.Id,
            new PayrollEntrySalaryPaymentRequest(
                PostingDate: new DateOnly(2026, 9, 10),
                Notes: "Moved salary"));
        Assert.True(moveResult.IsSuccess);

        // 3. Cash Voucher payment (-500 debit)
        var voucherResult = await cashVoucherService.AddAsync(new CashVoucherRequest(
            VoucherDate: new DateOnly(2026, 9, 12),
            Direction: CashDirection.Payment,
            CashboxId: 1,
            Amount: 500m,
            Description: "Cash voucher advance",
            EmployeeId: 1,
            EmployeeMovementType: EmployeeMovementType.Debit),
            cancellationToken: default);
        Assert.True(voucherResult.IsSuccess);

        // 4. Standalone Bonus movement (+300 credit)
        var bonusResult = await movementService.AddAsync(new EmployeeMovementRequest(
            EmployeeId: 1,
            Type: EmployeeMovementType.Bonus,
            Amount: 300m,
            Currency: CurrencyCode.EGP,
            MovementDate: new DateOnly(2026, 9, 15),
            Notes: "Project bonus"));
        Assert.True(bonusResult.IsSuccess);

        // Act 1: Statement
        var statementResult = await statementService.GetEmployeeStatementAsync(
            new PaginationRequest { PageNumber = 1, PageSize = 10 },
            new EmployeeStatementFilterRequest(EmployeeId: 1));

        // Act 2: Balance endpoint
        var balanceResult = await statementService.GetEmployeeBalanceAsync(1);

        // Act 3: Account summary
        var summaryResult = await statementService.GetEmployeeAccountSummaryAsync(1);

        // Assert 1: Exactly 4 transactions (no double counting)
        Assert.True(statementResult.IsSuccess);
        Assert.Equal(4, statementResult.Value.Items.Count);

        // Total Credits = 1000 + 2000 + 300 = 3300
        // Total Debits = 500
        // Net Balance = 2800
        Assert.Equal(3300m, statementResult.Value.Summary.TotalCredits);
        Assert.Equal(500m, statementResult.Value.Summary.TotalDebits);
        Assert.Equal(2800m, statementResult.Value.Summary.ClosingBalanceAmount);

        // Assert 2: Balance endpoint agrees
        Assert.True(balanceResult.IsSuccess);
        Assert.Equal(3300m, balanceResult.Value.TotalCredits);
        Assert.Equal(500m, balanceResult.Value.TotalDebits);
        Assert.Equal(2800m, balanceResult.Value.BalanceAmount);

        // Assert 3: Account summary agrees
        Assert.True(summaryResult.IsSuccess);
        Assert.Equal(3300m, summaryResult.Value.TotalCredits);
        Assert.Equal(500m, summaryResult.Value.TotalDebits);
        Assert.Equal(2800m, summaryResult.Value.CurrentBalance);
        Assert.Equal(1000m, summaryResult.Value.OpeningBalance);
        Assert.Equal(2000m, summaryResult.Value.TotalSalaryMoved);
        Assert.Equal(300m, summaryResult.Value.TotalBonuses);
        Assert.Equal(500m, summaryResult.Value.TotalAdvances);
    }

    [Fact]
    public async Task CompanyIsolation_TransactionsBelongingToAnotherCompany_NeverExposed()
    {
        // Arrange
        await using var dbCompany1 = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        await using var dbCompany2 = await PayrollEntryTestDatabase.CreateAsync(companyId: 2);

        var movementService2 = dbCompany2.CreateMovementService();
        var statementService1 = dbCompany1.CreateStatementService();

        // Add movement in Company 2 for Employee 1
        var result2 = await movementService2.AddAsync(new EmployeeMovementRequest(
            EmployeeId: 1,
            Type: EmployeeMovementType.Bonus,
            Amount: 999m,
            Currency: CurrencyCode.EGP,
            MovementDate: new DateOnly(2026, 9, 1),
            Notes: "Company 2 bonus"));
        Assert.True(result2.IsSuccess);

        // Act - Query statement in Company 1
        var statementResult1 = await statementService1.GetEmployeeStatementAsync(
            new PaginationRequest { PageNumber = 1, PageSize = 10 },
            new EmployeeStatementFilterRequest(EmployeeId: 1));

        // Assert - Company 1 should see 0 items
        Assert.True(statementResult1.IsSuccess);
        Assert.Empty(statementResult1.Value.Items);
        Assert.Equal(0m, statementResult1.Value.Summary.ClosingBalanceAmount);
    }
}
