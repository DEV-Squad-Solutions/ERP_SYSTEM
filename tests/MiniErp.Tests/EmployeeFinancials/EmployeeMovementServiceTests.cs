using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Features.EmployeeMovements;
using MiniErp.Domain.Entities.CashManagement;
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
    public async Task AddAsync_BonusAndDebit_ShouldSplitDebitCreditCorrectly()
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
    }

    [Fact]
    public async Task AddAsync_Advance_ShouldCreateEmployeeMovementAndCashVoucherAtomically()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var service = database.CreateMovementService();

        // Seed a Cashbox
        var cashbox = new Cashbox
        {
            CompanyId = 1,
            Code = "CB001",
            Name = "Main Safe",
            Currency = CurrencyCode.EGP,
            IsActive = true
        };
        database.Context.Cashboxes.Add(cashbox);
        await database.Context.SaveChangesAsync();

        // Act - Advance Movement
        var result = await service.AddAsync(new EmployeeMovementRequest(
            EmployeeId: 1,
            Type: EmployeeMovementType.Advance,
            Amount: 1000m,
            Currency: CurrencyCode.EGP,
            MovementDate: new DateOnly(2026, 8, 5),
            CashboxId: cashbox.Id,
            Notes: "Advance payment"));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1000m, result.Value.Debit);
        Assert.Equal(0m, result.Value.Credit);
        Assert.NotNull(result.Value.CashVoucherId);
        Assert.False(string.IsNullOrEmpty(result.Value.CashVoucherNumber));

        // Verify CashVoucher in database
        var voucher = await database.Context.CashVouchers
            .FirstOrDefaultAsync(v => v.Id == result.Value.CashVoucherId.Value);

        Assert.NotNull(voucher);
        Assert.Equal(CashDirection.Payment, voucher.Direction);
        Assert.Equal(CashPartyType.Employee, voucher.PartyType);
        Assert.Equal(1, voucher.EmployeeId);
        Assert.Equal(1000m, voucher.Amount);
        Assert.Equal(cashbox.Id, voucher.CashboxId);
        Assert.True(voucher.IsPosted);
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
}
