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
    public async Task AddAsync_BonusAndDebit_ShouldSplitDebitCreditAndCreateVouchersCorrectly()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var service = database.CreateMovementService();

        var cashbox = new Cashbox
        {
            CompanyId = 1,
            Code = "CB001",
            Name = "Main Safe",
            Currency = CurrencyCode.EGP,
            OpeningBalance = 1_000m,
            IsActive = true
        };
        database.Context.Cashboxes.Add(cashbox);
        await database.Context.SaveChangesAsync();

        // 1. Bonus Movement (Credit -> CashDirection.Receipt)
        var bonusResult = await service.AddAsync(new EmployeeMovementRequest(
            EmployeeId: 1,
            Type: EmployeeMovementType.Bonus,
            Amount: 500m,
            Currency: CurrencyCode.EGP,
            MovementDate: new DateOnly(2026, 8, 1),
            CashboxId: cashbox.Id,
            Notes: "Bonus for outstanding work"));

        // Assert Bonus
        Assert.True(bonusResult.IsSuccess);
        Assert.Equal(0m, bonusResult.Value.Debit);
        Assert.Equal(500m, bonusResult.Value.Credit);
        Assert.Equal(EmployeeMovementType.Bonus, bonusResult.Value.Type);
        Assert.NotNull(bonusResult.Value.CashVoucherId);
        Assert.StartsWith("RCV-", bonusResult.Value.CashVoucherNumber!);

        var bonusVoucher = await database.Context.CashVouchers
            .FirstOrDefaultAsync(v => v.Id == bonusResult.Value.CashVoucherId!.Value);
        Assert.NotNull(bonusVoucher);
        Assert.Equal(CashDirection.Receipt, bonusVoucher.Direction);
        Assert.Equal(CashPartyType.Employee, bonusVoucher.PartyType);
        Assert.Equal(1, bonusVoucher.EmployeeId);
        Assert.Equal(500m, bonusVoucher.Amount);
        Assert.Equal(CurrencyCode.EGP, bonusVoucher.Currency);
        Assert.True(bonusVoucher.IsPosted);

        // 2. Deduction Movement (Debit -> CashDirection.Payment)
        var deductionResult = await service.AddAsync(new EmployeeMovementRequest(
            EmployeeId: 1,
            Type: EmployeeMovementType.Deduction,
            Amount: 200m,
            Currency: CurrencyCode.EGP,
            MovementDate: new DateOnly(2026, 8, 2),
            CashboxId: cashbox.Id,
            Notes: "Late penalty deduction"));

        // Assert Deduction
        Assert.True(deductionResult.IsSuccess);
        Assert.Equal(200m, deductionResult.Value.Debit);
        Assert.Equal(0m, deductionResult.Value.Credit);
        Assert.Equal(EmployeeMovementType.Deduction, deductionResult.Value.Type);
        Assert.NotNull(deductionResult.Value.CashVoucherId);
        Assert.StartsWith("PAY-", deductionResult.Value.CashVoucherNumber!);

        var deductionVoucher = await database.Context.CashVouchers
            .FirstOrDefaultAsync(v => v.Id == deductionResult.Value.CashVoucherId!.Value);
        Assert.NotNull(deductionVoucher);
        Assert.Equal(CashDirection.Payment, deductionVoucher.Direction);
        Assert.Equal(CashPartyType.Employee, deductionVoucher.PartyType);
        Assert.Equal(1, deductionVoucher.EmployeeId);
        Assert.Equal(200m, deductionVoucher.Amount);
        Assert.Equal(CurrencyCode.EGP, deductionVoucher.Currency);
        Assert.True(deductionVoucher.IsPosted);
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
            OpeningBalance = 2_000m,
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
        Assert.StartsWith("PAY-", result.Value.CashVoucherNumber);

        // Verify CashVoucher in database
        var voucher = await database.Context.CashVouchers
            .FirstOrDefaultAsync(v => v.Id == result.Value.CashVoucherId!.Value);

        Assert.NotNull(voucher);
        Assert.Equal(CashDirection.Payment, voucher.Direction);
        Assert.Equal(CashPartyType.Employee, voucher.PartyType);
        Assert.Equal(1, voucher.EmployeeId);
        Assert.Equal(1000m, voucher.Amount);
        Assert.Equal(cashbox.Id, voucher.CashboxId);
        Assert.True(voucher.IsPosted);
    }

    [Fact]
    public async Task AddAsync_ShouldFail_WhenCashboxMissing()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var service = database.CreateMovementService();

        // Act
        var result = await service.AddAsync(new EmployeeMovementRequest(
            EmployeeId: 1,
            Type: EmployeeMovementType.Bonus,
            Amount: 500m,
            Currency: CurrencyCode.EGP,
            MovementDate: new DateOnly(2026, 8, 1),
            CashboxId: null));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("EmployeeMovements.CashboxRequired", result.Error.Code);
    }

    [Fact]
    public async Task AddAsync_ShouldFail_WhenCashboxNotFound()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var service = database.CreateMovementService();

        // Act
        var result = await service.AddAsync(new EmployeeMovementRequest(
            EmployeeId: 1,
            Type: EmployeeMovementType.Advance,
            Amount: 500m,
            Currency: CurrencyCode.EGP,
            MovementDate: new DateOnly(2026, 8, 1),
            CashboxId: 9999));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("EmployeeMovements.CashboxNotFound", result.Error.Code);
    }

    [Fact]
    public async Task AddAsync_ShouldFail_WhenCashboxInactive()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var service = database.CreateMovementService();

        var cashbox = new Cashbox
        {
            CompanyId = 1,
            Code = "CB-INACTIVE",
            Name = "Inactive Cashbox",
            Currency = CurrencyCode.EGP,
            IsActive = false
        };
        database.Context.Cashboxes.Add(cashbox);
        await database.Context.SaveChangesAsync();

        // Act
        var result = await service.AddAsync(new EmployeeMovementRequest(
            EmployeeId: 1,
            Type: EmployeeMovementType.Bonus,
            Amount: 300m,
            Currency: CurrencyCode.EGP,
            MovementDate: new DateOnly(2026, 8, 1),
            CashboxId: cashbox.Id));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("EmployeeMovements.CashboxInactive", result.Error.Code);
    }

    [Fact]
    public async Task AddAsync_ShouldFail_WhenCashboxNotEgp()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var service = database.CreateMovementService();

        var cashbox = new Cashbox
        {
            CompanyId = 1,
            Code = "CB-USD",
            Name = "USD Safe",
            Currency = CurrencyCode.USD,
            IsActive = true
        };
        database.Context.Cashboxes.Add(cashbox);
        await database.Context.SaveChangesAsync();

        // Act
        var result = await service.AddAsync(new EmployeeMovementRequest(
            EmployeeId: 1,
            Type: EmployeeMovementType.Withdrawal,
            Amount: 100m,
            Currency: CurrencyCode.EGP,
            MovementDate: new DateOnly(2026, 8, 1),
            CashboxId: cashbox.Id));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("EmployeeMovements.CashboxMustBeEgp", result.Error.Code);
    }

    [Fact]
    public async Task AddAsync_Payment_ShouldFail_WhenInsufficientCashboxBalance()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var service = database.CreateMovementService();

        var cashbox = new Cashbox
        {
            CompanyId = 1,
            Code = "CB-LOW",
            Name = "Low Balance Safe",
            Currency = CurrencyCode.EGP,
            OpeningBalance = 100m,
            IsActive = true
        };
        database.Context.Cashboxes.Add(cashbox);
        await database.Context.SaveChangesAsync();

        // Act - Attempt to withdraw 500 from a cashbox with 100
        var result = await service.AddAsync(new EmployeeMovementRequest(
            EmployeeId: 1,
            Type: EmployeeMovementType.Withdrawal,
            Amount: 500m,
            Currency: CurrencyCode.EGP,
            MovementDate: new DateOnly(2026, 8, 1),
            CashboxId: cashbox.Id));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("CashVouchers.InsufficientBalance", result.Error.Code);
        Assert.Equal($"Cashbox {cashbox.Id} does not have enough balance.", result.Error.Description);

        // Verify nothing was created in DB
        Assert.Empty(await database.Context.EmployeeMovements.ToListAsync());
        Assert.Empty(await database.Context.CashVouchers.ToListAsync());
    }

    [Fact]
    public async Task AddAsync_ForeignCurrency_ShouldCreateEgpCashVoucherWithConvertedAmount()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var service = database.CreateMovementService();

        var cashbox = new Cashbox
        {
            CompanyId = 1,
            Code = "CB-MAIN",
            Name = "Main Safe",
            Currency = CurrencyCode.EGP,
            OpeningBalance = 10_000m,
            IsActive = true
        };
        database.Context.Cashboxes.Add(cashbox);
        await database.Context.SaveChangesAsync();

        // Act - Foreign currency advance: 100 USD @ rate 50 = 5,000 EGP
        var result = await service.AddAsync(new EmployeeMovementRequest(
            EmployeeId: 1,
            Type: EmployeeMovementType.Advance,
            Amount: 100m,
            Currency: CurrencyCode.USD,
            ExchangeRate: 50m,
            MovementDate: new DateOnly(2026, 8, 1),
            CashboxId: cashbox.Id));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(100m, result.Value.Debit);
        Assert.Equal(50m, result.Value.ExchangeRate);
        Assert.Equal(5_000m, result.Value.BaseDebit);
        Assert.Equal(CurrencyCode.USD, result.Value.Currency);

        var voucher = await database.Context.CashVouchers
            .FirstOrDefaultAsync(v => v.Id == result.Value.CashVoucherId!.Value);
        Assert.NotNull(voucher);
        Assert.Equal(CurrencyCode.EGP, voucher.Currency);
        Assert.Equal(5_000m, voucher.Amount);
        Assert.Equal(1m, voucher.ExchangeRate);
        Assert.Equal(CashDirection.Payment, voucher.Direction);
    }

    [Fact]
    public async Task AddBulkAsync_ShouldCreateAllMovementsInOneTransaction()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var service = database.CreateMovementService();

        var cashbox = new Cashbox
        {
            CompanyId = 1,
            Code = "CB-BULK",
            Name = "Bulk Safe",
            Currency = CurrencyCode.EGP,
            OpeningBalance = 1_000m,
            IsActive = true
        };
        database.Context.Cashboxes.Add(cashbox);
        await database.Context.SaveChangesAsync();

        var bulkRequest = new BulkEmployeeMovementRequest(
        [
            new EmployeeMovementRequest(
                EmployeeId: 1,
                Type: EmployeeMovementType.Bonus,
                Amount: 300m,
                Currency: CurrencyCode.EGP,
                MovementDate: new DateOnly(2026, 8, 1),
                CashboxId: cashbox.Id),
            new EmployeeMovementRequest(
                EmployeeId: 2,
                Type: EmployeeMovementType.Deduction,
                Amount: 150m,
                Currency: CurrencyCode.EGP,
                MovementDate: new DateOnly(2026, 8, 1),
                CashboxId: cashbox.Id)
        ]);

        // Act
        var result = await service.AddBulkAsync(bulkRequest);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);

        var count = await database.Context.EmployeeMovements.CountAsync(m => m.CompanyId == 1);
        Assert.Equal(2, count);

        var voucherCount = await database.Context.CashVouchers.CountAsync(v => v.CompanyId == 1);
        Assert.Equal(2, voucherCount);
    }
}
