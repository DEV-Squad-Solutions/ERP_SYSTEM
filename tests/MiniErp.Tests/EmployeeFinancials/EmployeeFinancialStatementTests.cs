using MiniErp.Application.Common.Models;
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
            StartDate: new DateOnly(2026, 8, 1),
            EndDate: new DateOnly(2026, 8, 10),
            EmployeeId: 1));
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

        // 3. Movement: Cash Advance (Debit -500) on 2026-08-15
        var advance = new EmployeeMovement
        {
            CompanyId = 1,
            EmployeeId = 1,
            CashVoucherId = 9001,
            Type = EmployeeMovementType.Advance,
            MovementDate = new DateOnly(2026, 8, 15),
            Currency = CurrencyCode.EGP,
            Notes = "Mid-month advance"
        };
        advance.ApplyAmounts(EmployeeMovementType.Advance, 500m);
        advance.ApplyExchangeRate(1m);
        database.Context.EmployeeMovements.Add(advance);

        // 4. Movement: Performance Bonus (Credit +1,000) on 2026-08-20
        var bonus = new EmployeeMovement
        {
            CompanyId = 1,
            EmployeeId = 1,
            CashVoucherId = 9002,
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

        // Item 3: Advance (-500) -> Running Balance = 6,500
        Assert.Equal(EmployeeStatementSourceType.CashVoucher, response.Items[2].SourceType);
        Assert.Equal(500m, response.Items[2].DebitAmount);
        Assert.Equal(0m, response.Items[2].CreditAmount);
        Assert.Equal(6500m, response.Items[2].BalanceAmount);

        // Item 4: Bonus (+1000) -> Running Balance = 7,500
        Assert.Equal(EmployeeStatementSourceType.CashVoucher, response.Items[3].SourceType);
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
        Assert.Equal(500m, summaryResult.Value.TotalAdvances);
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
}
