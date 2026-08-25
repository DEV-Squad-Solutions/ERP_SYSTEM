using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Features.EmployeeTransactions;
using MiniErp.Domain.Entities.Employees;
using MiniErp.Domain.Enums;
using MiniErp.Tests.PayrollEntries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace MiniErp.Tests.EmployeeTransactions;

public sealed class EmployeeTransactionServiceTests
{
    [Fact]
    public async Task AddAsync_ShouldCreateManualCreditEntry_AndComputeRunningBalance()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var service = database.CreateTransactionService();

        var request = new EmployeeAccountEntryRequest(
            EmployeeId: 1,
            Type: EmployeeTransactionType.Credit,
            Amount: 1500m,
            TransactionDate: new DateOnly(2026, 8, 1),
            CashboxId: 1,
            CashMovementTypeId: 1,
            Notes: "Initial manual credit bonus");

        // Act
        var result = await service.AddAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.EmployeeId);
        Assert.Equal(1500m, result.Value.Amount);
        Assert.Equal(1500m, result.Value.RunningBalance);
        Assert.Equal(EmployeeTransactionType.Credit, result.Value.Type);
        Assert.Equal(EmployeeTransactionSource.Manual, result.Value.SourceType);
        Assert.True(result.Value.CashVoucherId > 0);
        Assert.Equal(1, result.Value.CashBoxId);
    }

    [Fact]
    public async Task AddBulkAsync_ShouldCreateMultipleEntries_AndComputeRunningBalancesAccurately()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var service = database.CreateTransactionService();

        var bulkRequest = new BulkEmployeeAccountEntryRequest(
            Entries:
            [
                new IndividualEmployeeAccountEntryRequest(
                    EmployeeId: 1,
                    Type: EmployeeTransactionType.Credit,
                    Amount: 2000m,
                    TransactionDate: new DateOnly(2026, 8, 1),
                    CashboxId: 1,
                    CashMovementTypeId: 1,
                    Notes: "Credit 1"),
                new IndividualEmployeeAccountEntryRequest(
                    EmployeeId: 1,
                    Type: EmployeeTransactionType.Debit,
                    Amount: 500m,
                    TransactionDate: new DateOnly(2026, 8, 2),
                    CashboxId: 1,
                    CashMovementTypeId: 2,
                    Notes: "Debit 1"),
                new IndividualEmployeeAccountEntryRequest(
                    EmployeeId: 2,
                    Type: EmployeeTransactionType.Bonus,
                    Amount: 300m,
                    TransactionDate: new DateOnly(2026, 8, 1),
                    CashboxId: 1,
                    CashMovementTypeId: 1,
                    Notes: "Bonus 1")
            ]);

        // Act
        var result = await service.AddBulkAsync(bulkRequest);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Count);

        var emp1Entries = result.Value.Where(e => e.EmployeeId == 1).ToList();
        Assert.Equal(2, emp1Entries.Count);
        Assert.Equal(2000m, emp1Entries[0].RunningBalance);
        Assert.Equal(1500m, emp1Entries[1].RunningBalance); // 2000 - 500

        var emp2Entry = result.Value.First(e => e.EmployeeId == 2);
        Assert.Equal(300m, emp2Entry.RunningBalance);
    }

    [Fact]
    public async Task GetBalanceAsync_ShouldReturnTotalCreditsDebitsAndNetBalance()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var service = database.CreateTransactionService();

        await service.AddAsync(new EmployeeAccountEntryRequest(
            EmployeeId: 1,
            Type: EmployeeTransactionType.Credit,
            Amount: 5000m,
            TransactionDate: new DateOnly(2026, 8, 1),
            CashboxId: 1,
            CashMovementTypeId: 1));

        await service.AddAsync(new EmployeeAccountEntryRequest(
            EmployeeId: 1,
            Type: EmployeeTransactionType.Deduction,
            Amount: 1200m,
            TransactionDate: new DateOnly(2026, 8, 5),
            CashboxId: 1,
            CashMovementTypeId: 2));

        // Act
        var result = await service.GetBalanceAsync(employeeId: 1);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.EmployeeId);
        Assert.Equal(5000m, result.Value.TotalCredit);
        Assert.Equal(1200m, result.Value.TotalDebit);
        Assert.Equal(3800m, result.Value.Balance); // 5000 - 1200
    }

    [Fact]
    public async Task GetStatementAsync_ShouldReturnDetailedStatementWithOpeningAndClosingBalances()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var service = database.CreateTransactionService();

        // Prior transaction (before statement window)
        await service.AddAsync(new EmployeeAccountEntryRequest(
            EmployeeId: 1,
            Type: EmployeeTransactionType.Credit,
            Amount: 1000m,
            TransactionDate: new DateOnly(2026, 7, 15),
            CashboxId: 1,
            CashMovementTypeId: 1));

        // Transactions inside statement window
        await service.AddAsync(new EmployeeAccountEntryRequest(
            EmployeeId: 1,
            Type: EmployeeTransactionType.Bonus,
            Amount: 400m,
            TransactionDate: new DateOnly(2026, 8, 2),
            CashboxId: 1,
            CashMovementTypeId: 1));

        await service.AddAsync(new EmployeeAccountEntryRequest(
            EmployeeId: 1,
            Type: EmployeeTransactionType.Debit,
            Amount: 200m,
            TransactionDate: new DateOnly(2026, 8, 10),
            CashboxId: 1,
            CashMovementTypeId: 2));

        // Act
        var result = await service.GetStatementAsync(
            employeeId: 1,
            fromDate: new DateOnly(2026, 8, 1),
            toDate: new DateOnly(2026, 8, 31));

        // Assert
        Assert.True(result.IsSuccess);
        var summary = result.Value.Summary;
        Assert.Equal(1000m, summary.OpeningBalance);
        Assert.Equal(400m, summary.TotalCredit);
        Assert.Equal(200m, summary.TotalDebit);
        Assert.Equal(1200m, summary.ClosingBalance); // 1000 + 400 - 200
        Assert.Equal(2, summary.TotalTransactions);
        Assert.Equal(2, result.Value.Transactions.Count);
        Assert.True(result.Value.Transactions[0].CashVoucherId > 0);
        Assert.Equal(1, result.Value.Transactions[0].CashBoxId);
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateNonVoucherTransaction()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var service = database.CreateTransactionService();

        var addResult = await service.AddAsync(new EmployeeAccountEntryRequest(
            EmployeeId: 1,
            Type: EmployeeTransactionType.Credit,
            Amount: 500m,
            TransactionDate: new DateOnly(2026, 8, 1),
            CashboxId: 1,
            CashMovementTypeId: 1,
            Notes: "Original"));

        Assert.True(addResult.IsSuccess);
        var id = addResult.Value.Id;

        // Act
        var updateResult = await service.UpdateAsync(
            id,
            new EmployeeTransactionUpdateRequest(
                Amount: 750m,
                TransactionDate: new DateOnly(2026, 8, 2),
                Notes: "Updated"));

        // Assert
        Assert.True(updateResult.IsSuccess);
        Assert.Equal(750m, updateResult.Value.Amount);
        Assert.Equal(new DateOnly(2026, 8, 2), updateResult.Value.TransactionDate);
        Assert.Equal("Updated", updateResult.Value.Notes);
    }

    [Fact]
    public async Task DeleteAsync_ShouldFail_WhenLinkedToPayroll()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var service = database.CreateTransactionService();

        var postResult = await service.PostSalaryCreditAsync(
            employeeId: 1,
            amount: 3000m,
            payrollEntryId: 99,
            transactionDate: new DateOnly(2026, 8, 10),
            cashboxId: 1,
            cashMovementTypeId: 1);

        Assert.True(postResult.IsSuccess);
        var id = postResult.Value.Id;

        // Act
        var deleteResult = await service.DeleteAsync(id);

        // Assert
        Assert.True(deleteResult.IsFailure);
        Assert.Equal("EmployeeTransaction.PayrollPosted", deleteResult.Error.Code);
    }
}
