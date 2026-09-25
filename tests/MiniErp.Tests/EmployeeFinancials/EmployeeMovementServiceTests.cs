using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Features.EmployeeMovements;
using MiniErp.Application.Features.EmployeeOpeningBalances;
using MiniErp.Domain.Entities.Employees;
using MiniErp.Domain.Enums;
using MiniErp.Tests.PayrollEntries;
using System;
using System.Threading.Tasks;
using Xunit;

namespace MiniErp.Tests.EmployeeFinancials;

public sealed class EmployeeMovementServiceTests
{
    [Fact]
    public async Task AddAsync_BonusAndDeduction_ShouldSplitDebitCreditCorrectly()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var service = database.CreateMovementService();

        // 1. Bonus Movement (Credit)
        var bonusResult = await service.AddAsync(new EmployeeMovementRequest(
            EmployeeId: 1,
            Type: EmployeeMovementType.Bonus,
            Amount: 500m,
            Currency: CurrencyCode.EGP,
            MovementDate: new DateOnly(2026, 8, 1),
            Notes: "Bonus for outstanding work"));

        // Assert Bonus
        Assert.True(bonusResult.IsSuccess);
        Assert.Equal(0m, bonusResult.Value.Debit);
        Assert.Equal(500m, bonusResult.Value.Credit);
        Assert.Equal(EmployeeMovementType.Bonus, bonusResult.Value.Type);
        Assert.Null(bonusResult.Value.CashVoucherId);

        // 2. Deduction Movement (Debit)
        var deductionResult = await service.AddAsync(new EmployeeMovementRequest(
            EmployeeId: 1,
            Type: EmployeeMovementType.Deduction,
            Amount: 200m,
            Currency: CurrencyCode.EGP,
            MovementDate: new DateOnly(2026, 8, 2),
            Notes: "Late penalty deduction"));

        // Assert Deduction
        Assert.True(deductionResult.IsSuccess);
        Assert.Equal(200m, deductionResult.Value.Debit);
        Assert.Equal(0m, deductionResult.Value.Credit);
        Assert.Equal(EmployeeMovementType.Deduction, deductionResult.Value.Type);
        Assert.Null(deductionResult.Value.CashVoucherId);

        // Verify database records
        var movements = await database.Context.EmployeeMovements
            .Where(m => m.CompanyId == 1 && m.EmployeeId == 1)
            .OrderBy(m => m.MovementDate)
            .ToListAsync();

        Assert.Equal(2, movements.Count);
        Assert.Equal(500m, movements[0].Credit);
        Assert.Equal(200m, movements[1].Debit);
    }

    [Fact]
    public async Task AddAsync_DebitAndCredit_ShouldPersistAccountMovementsAtomically()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var service = database.CreateMovementService();

        // Act - Debit Movement
        var debitResult = await service.AddAsync(new EmployeeMovementRequest(
            EmployeeId: 1,
            Type: EmployeeMovementType.Debit,
            Amount: 1000m,
            Currency: CurrencyCode.EGP,
            MovementDate: new DateOnly(2026, 8, 5),
            Notes: "Debit payment"));

        // Assert Debit
        Assert.True(debitResult.IsSuccess);
        Assert.Equal(1000m, debitResult.Value.Debit);
        Assert.Equal(0m, debitResult.Value.Credit);
        Assert.Null(debitResult.Value.CashVoucherId);

        // Act - Credit Movement
        var creditResult = await service.AddAsync(new EmployeeMovementRequest(
            EmployeeId: 1,
            Type: EmployeeMovementType.Credit,
            Amount: 750m,
            Currency: CurrencyCode.EGP,
            MovementDate: new DateOnly(2026, 8, 6),
            Notes: "Credit payment"));

        // Assert Credit
        Assert.True(creditResult.IsSuccess);
        Assert.Equal(0m, creditResult.Value.Debit);
        Assert.Equal(750m, creditResult.Value.Credit);
        Assert.Null(creditResult.Value.CashVoucherId);

        var saved = await database.Context.EmployeeMovements
            .Where(m => m.CompanyId == 1 && m.EmployeeId == 1)
            .ToListAsync();
        Assert.Equal(2, saved.Count);
    }

    [Fact]
    public async Task AddAsync_ShouldFail_WhenEmployeeNotFound()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var service = database.CreateMovementService();

        // Act
        var result = await service.AddAsync(new EmployeeMovementRequest(
            EmployeeId: 9999,
            Type: EmployeeMovementType.Bonus,
            Amount: 500m,
            Currency: CurrencyCode.EGP,
            MovementDate: new DateOnly(2026, 8, 1)));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("EmployeeMovements.EmployeeNotFound", result.Error.Code);
    }

    [Fact]
    public async Task AddAsync_ForeignCurrency_ShouldCalculateBaseDebitAndCreditCorrectly()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var service = database.CreateMovementService();

        // Act - Foreign currency debit: 100 USD @ rate 50 = 5,000 EGP base
        var result = await service.AddAsync(new EmployeeMovementRequest(
            EmployeeId: 1,
            Type: EmployeeMovementType.Debit,
            Amount: 100m,
            Currency: CurrencyCode.USD,
            ExchangeRate: 50m,
            MovementDate: new DateOnly(2026, 8, 1),
            Notes: "USD debit"));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(100m, result.Value.Debit);
        Assert.Equal(50m, result.Value.ExchangeRate);
        Assert.Equal(5_000m, result.Value.BaseDebit);
        Assert.Equal(CurrencyCode.USD, result.Value.Currency);
        Assert.Null(result.Value.CashVoucherId);
    }

    [Fact]
    public async Task AddBulkAsync_ShouldCreateAllMovementsInOneTransaction()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var service = database.CreateMovementService();

        var bulkRequest = new BulkEmployeeMovementRequest(
        [
            new EmployeeMovementRequest(
                EmployeeId: 1,
                Type: EmployeeMovementType.Bonus,
                Amount: 300m,
                Currency: CurrencyCode.EGP,
                MovementDate: new DateOnly(2026, 8, 1)),
            new EmployeeMovementRequest(
                EmployeeId: 2,
                Type: EmployeeMovementType.Deduction,
                Amount: 150m,
                Currency: CurrencyCode.EGP,
                MovementDate: new DateOnly(2026, 8, 1))
        ]);

        // Act
        var result = await service.AddBulkAsync(bulkRequest);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);

        var count = await database.Context.EmployeeMovements.CountAsync(m => m.CompanyId == 1);
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnCreatedMovement()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var service = database.CreateMovementService();

        var addResult = await service.AddAsync(new EmployeeMovementRequest(
            EmployeeId: 1,
            Type: EmployeeMovementType.Bonus,
            Amount: 500m,
            Currency: CurrencyCode.EGP,
            MovementDate: new DateOnly(2026, 8, 1),
            Notes: "Special recognition"));
        Assert.True(addResult.IsSuccess);

        // Act
        var getResult = await service.GetByIdAsync(addResult.Value.Id);

        // Assert
        Assert.True(getResult.IsSuccess);
        Assert.Equal(addResult.Value.Id, getResult.Value.Id);
        Assert.Equal(500m, getResult.Value.Credit);
        Assert.Equal(0m, getResult.Value.Debit);
        Assert.Equal("Special recognition", getResult.Value.Notes);
    }

    [Fact]
    public async Task GetReportAsync_ShouldCalculateDebitsAndCreditsCorrectly()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var service = database.CreateMovementService();

        await service.AddAsync(new EmployeeMovementRequest(
            EmployeeId: 1,
            Type: EmployeeMovementType.Debit,
            Amount: 300m,
            Currency: CurrencyCode.EGP,
            MovementDate: new DateOnly(2026, 8, 1)));

        await service.AddAsync(new EmployeeMovementRequest(
            EmployeeId: 1,
            Type: EmployeeMovementType.Bonus,
            Amount: 500m,
            Currency: CurrencyCode.EGP,
            MovementDate: new DateOnly(2026, 8, 2)));

        await service.AddAsync(new EmployeeMovementRequest(
            EmployeeId: 1,
            Type: EmployeeMovementType.Deduction,
            Amount: 100m,
            Currency: CurrencyCode.EGP,
            MovementDate: new DateOnly(2026, 8, 3)));

        // Act
        var reportResult = await service.GetReportAsync(new EmployeeMovementReportRequest(
            EmployeeId: 1));

        // Assert
        Assert.True(reportResult.IsSuccess);
        Assert.Equal(3, reportResult.Value.Summary.TotalMovements);
        Assert.Equal(400m, reportResult.Value.Summary.TotalDebits); // 300 + 100
        Assert.Equal(500m, reportResult.Value.Summary.TotalCredits); // 500
        Assert.Equal(500m, reportResult.Value.Summary.TotalBonuses);
        Assert.Equal(100m, reportResult.Value.Summary.TotalDeductions);
    }

    [Fact]
    public async Task DeleteDebit_ShouldRestoreAccountBalance()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var movementService = database.CreateMovementService();
        var statementService = database.CreateStatementService();
        var openingBalanceService = database.CreateOpeningBalanceService();

        // Initial balance: 10,000 Credit
        await openingBalanceService.AddAsync(new EmployeeOpeningBalanceRequest(
            EmployeeId: 1,
            DocumentDate: new DateOnly(2026, 8, 1),
            Currency: CurrencyCode.EGP,
            BalanceType: EmployeeBalanceType.Credit,
            Amount: 10_000m,
            Notes: "Initial balance"));

        var initialBalance = await statementService.GetEmployeeBalanceAsync(1);
        Assert.Equal(10_000m, initialBalance.Value.BalanceAmount);
        Assert.Equal(10_000m, initialBalance.Value.TotalCredits);
        Assert.Equal(0m, initialBalance.Value.TotalDebits);

        // Act 1: Create Debit Movement = 2,000
        var debitResult = await movementService.AddAsync(new EmployeeMovementRequest(
            EmployeeId: 1,
            Type: EmployeeMovementType.Debit,
            Amount: 2_000m,
            Currency: CurrencyCode.EGP,
            MovementDate: new DateOnly(2026, 8, 2),
            Notes: "Advance"));
        Assert.True(debitResult.IsSuccess);

        // Balance after movement: 8,000
        var afterMovementBalance = await statementService.GetEmployeeBalanceAsync(1);
        Assert.Equal(8_000m, afterMovementBalance.Value.BalanceAmount);
        Assert.Equal(10_000m, afterMovementBalance.Value.TotalCredits);
        Assert.Equal(2_000m, afterMovementBalance.Value.TotalDebits);

        // Act 2: Delete Debit Movement
        var deleteResult = await movementService.DeleteAsync(debitResult.Value.Id);
        Assert.True(deleteResult.IsSuccess);

        // Balance after deletion: restored to 10,000
        var afterDeleteBalance = await statementService.GetEmployeeBalanceAsync(1);
        Assert.Equal(10_000m, afterDeleteBalance.Value.BalanceAmount);
        Assert.Equal(10_000m, afterDeleteBalance.Value.TotalCredits);
        Assert.Equal(0m, afterDeleteBalance.Value.TotalDebits);
    }

    [Fact]
    public async Task DeleteCredit_ShouldRestoreAccountBalance()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var movementService = database.CreateMovementService();
        var statementService = database.CreateStatementService();
        var openingBalanceService = database.CreateOpeningBalanceService();

        // Initial balance: 10,000 Credit
        await openingBalanceService.AddAsync(new EmployeeOpeningBalanceRequest(
            EmployeeId: 1,
            DocumentDate: new DateOnly(2026, 8, 1),
            Currency: CurrencyCode.EGP,
            BalanceType: EmployeeBalanceType.Credit,
            Amount: 10_000m,
            Notes: "Initial balance"));

        // Act 1: Create Credit Movement = 2,000
        var creditResult = await movementService.AddAsync(new EmployeeMovementRequest(
            EmployeeId: 1,
            Type: EmployeeMovementType.Credit,
            Amount: 2_000m,
            Currency: CurrencyCode.EGP,
            MovementDate: new DateOnly(2026, 8, 2),
            Notes: "Credit payment"));
        Assert.True(creditResult.IsSuccess);

        // Balance after movement: 12,000
        var afterMovementBalance = await statementService.GetEmployeeBalanceAsync(1);
        Assert.Equal(12_000m, afterMovementBalance.Value.BalanceAmount);
        Assert.Equal(12_000m, afterMovementBalance.Value.TotalCredits);

        // Act 2: Delete Credit Movement
        var deleteResult = await movementService.DeleteAsync(creditResult.Value.Id);
        Assert.True(deleteResult.IsSuccess);

        // Balance after deletion: restored to 10,000
        var afterDeleteBalance = await statementService.GetEmployeeBalanceAsync(1);
        Assert.Equal(10_000m, afterDeleteBalance.Value.BalanceAmount);
        Assert.Equal(10_000m, afterDeleteBalance.Value.TotalCredits);
    }

    [Fact]
    public async Task DeleteDeduction_ShouldRestoreAccountBalance()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var movementService = database.CreateMovementService();
        var statementService = database.CreateStatementService();
        var openingBalanceService = database.CreateOpeningBalanceService();

        // Initial balance: 10,000 Credit
        await openingBalanceService.AddAsync(new EmployeeOpeningBalanceRequest(
            EmployeeId: 1,
            DocumentDate: new DateOnly(2026, 8, 1),
            Currency: CurrencyCode.EGP,
            BalanceType: EmployeeBalanceType.Credit,
            Amount: 10_000m,
            Notes: "Initial balance"));

        // Act 1: Create Deduction Movement = 2,000 (Debit effect)
        var deductionResult = await movementService.AddAsync(new EmployeeMovementRequest(
            EmployeeId: 1,
            Type: EmployeeMovementType.Deduction,
            Amount: 2_000m,
            Currency: CurrencyCode.EGP,
            MovementDate: new DateOnly(2026, 8, 2),
            Notes: "Penalty deduction"));
        Assert.True(deductionResult.IsSuccess);

        // Balance after deduction: 8,000
        var afterMovementBalance = await statementService.GetEmployeeBalanceAsync(1);
        Assert.Equal(8_000m, afterMovementBalance.Value.BalanceAmount);
        Assert.Equal(2_000m, afterMovementBalance.Value.TotalDebits);

        // Act 2: Delete Deduction Movement
        var deleteResult = await movementService.DeleteAsync(deductionResult.Value.Id);
        Assert.True(deleteResult.IsSuccess);

        // Balance after deletion: restored to 10,000
        var afterDeleteBalance = await statementService.GetEmployeeBalanceAsync(1);
        Assert.Equal(10_000m, afterDeleteBalance.Value.BalanceAmount);
        Assert.Equal(0m, afterDeleteBalance.Value.TotalDebits);
    }

    [Fact]
    public async Task DeleteBonus_ShouldRestoreAccountBalance()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var movementService = database.CreateMovementService();
        var statementService = database.CreateStatementService();
        var openingBalanceService = database.CreateOpeningBalanceService();

        // Initial balance: 10,000 Credit
        await openingBalanceService.AddAsync(new EmployeeOpeningBalanceRequest(
            EmployeeId: 1,
            DocumentDate: new DateOnly(2026, 8, 1),
            Currency: CurrencyCode.EGP,
            BalanceType: EmployeeBalanceType.Credit,
            Amount: 10_000m,
            Notes: "Initial balance"));

        // Act 1: Create Bonus = 2,000 (Credit effect)
        var bonusResult = await movementService.AddAsync(new EmployeeMovementRequest(
            EmployeeId: 1,
            Type: EmployeeMovementType.Bonus,
            Amount: 2_000m,
            Currency: CurrencyCode.EGP,
            MovementDate: new DateOnly(2026, 8, 2),
            Notes: "Performance bonus"));
        Assert.True(bonusResult.IsSuccess);

        // Balance after bonus: 12,000
        var afterMovementBalance = await statementService.GetEmployeeBalanceAsync(1);
        Assert.Equal(12_000m, afterMovementBalance.Value.BalanceAmount);
        Assert.Equal(12_000m, afterMovementBalance.Value.TotalCredits);

        // Act 2: Delete Bonus
        var deleteResult = await movementService.DeleteAsync(bonusResult.Value.Id);
        Assert.True(deleteResult.IsSuccess);

        // Balance after deletion: restored to 10,000
        var afterDeleteBalance = await statementService.GetEmployeeBalanceAsync(1);
        Assert.Equal(10_000m, afterDeleteBalance.Value.BalanceAmount);
        Assert.Equal(10_000m, afterDeleteBalance.Value.TotalCredits);
    }

    [Fact]
    public async Task DeleteAsync_ShouldFail_WhenMovementHasCashVoucherDependency()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var movementService = database.CreateMovementService();

        // Create movement linked to a CashVoucher
        var movement = new EmployeeMovement
        {
            CompanyId = 1,
            EmployeeId = 1,
            CashVoucherId = 42,
            Type = EmployeeMovementType.Debit,
            MovementDate = new DateOnly(2026, 8, 5),
            Currency = CurrencyCode.EGP,
            Notes = "Movement generated from voucher"
        };
        movement.ApplyAmounts(EmployeeMovementType.Debit, 1000m);
        database.Context.EmployeeMovements.Add(movement);
        await database.Context.SaveChangesAsync();

        // Act: attempt to delete the movement directly
        var result = await movementService.DeleteAsync(movement.Id);

        // Assert: must fail with Conflict / LinkedToCashVoucher
        Assert.True(result.IsFailure);
        Assert.Equal("EmployeeMovements.LinkedToCashVoucher", result.Error.Code);

        // Ensure movement still exists in DB
        var exists = await database.Context.EmployeeMovements.AnyAsync(m => m.Id == movement.Id);
        Assert.True(exists);
    }

    [Fact]
    public async Task DeleteAsync_ShouldFail_WhenMovementDoesNotExist()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var movementService = database.CreateMovementService();

        // Act
        var result = await movementService.DeleteAsync(99999);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("EmployeeMovements.NotFound", result.Error.Code);
    }
}
