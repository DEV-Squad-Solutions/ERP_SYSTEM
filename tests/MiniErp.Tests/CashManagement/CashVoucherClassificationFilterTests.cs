using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Mappings;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.CashVouchers;
using MiniErp.Application.Features.Statements;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure;

namespace MiniErp.Tests.CashManagement;

public sealed class CashVoucherClassificationFilterTests
{
    static CashVoucherClassificationFilterTests()
    {
        MappingConfiguration.Register(
            typeof(InfrastructureAssemblyMarker).Assembly);
    }

    [Fact]
    public async Task ExpenseFilterIncludesDirectAccountVoucherAlongsideMovementTypeVoucher()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var service = database.CreateVoucherService(companyId: 1);

        var directExpense = await AddPostedVoucherAsync(
            service,
            CashDirection.Payment,
            movementTypeId: null,
            accountId: 2,
            amount: 30m);
        var movementExpense = await AddPostedVoucherAsync(
            service,
            CashDirection.Payment,
            movementTypeId: 10,
            accountId: 2,
            amount: 20m);
        var directRevenue = await AddPostedVoucherAsync(
            service,
            CashDirection.Receipt,
            movementTypeId: null,
            accountId: 1,
            amount: 50m);
        await database.Context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO CashVouchers (
                Id, CompanyId, VoucherNumber, VoucherDate, Direction,
                CashboxId, CashMovementTypeId, AccountId, PartyType,
                Amount, Currency, ExchangeRate, BaseAmount, IsPosted,
                LastModifiedAt, CreatedById, CreatedOn, CreatedByPc, IsDeleted)
            VALUES
                (901, 1, 'CLF-901', '2026-07-27', 2, 1, NULL, 2, 1,
                 7, 1, 1, 7, 0, '2026-07-27', 'test', '2026-07-27', 'test', 0);
            """);
        database.Context.ChangeTracker.Clear();

        Assert.True(directExpense.IsSuccess);
        Assert.True(movementExpense.IsSuccess);
        Assert.True(directRevenue.IsSuccess);
        Assert.Equal(
            CashMovementClassification.Expense,
            directExpense.Value.Classification);
        Assert.Equal(
            CashMovementClassification.Expense,
            movementExpense.Value.Classification);
        Assert.Equal(
            CashMovementClassification.Revenue,
            directRevenue.Value.Classification);

        var postedOnly = await service.GetAllAsync(
            new PaginationRequest { PageNumber = 1, PageSize = 20 },
            new CashVoucherFilterRequest(
                Classification: CashMovementClassification.Expense,
                IsDraft: false));

        Assert.True(postedOnly.IsSuccess);
        Assert.Contains(
            postedOnly.Value.Items,
            item => item.Id == directExpense.Value.Id);
        Assert.Contains(
            postedOnly.Value.Items,
            item => item.Id == movementExpense.Value.Id);
        Assert.DoesNotContain(
            postedOnly.Value.Items,
            item => item.Id == directRevenue.Value.Id);
        Assert.DoesNotContain(
            postedOnly.Value.Items,
            item => item.VoucherNumber == "CLF-901");

        var cashboxStatement = await database.CreateStatementService(1)
            .GetCashboxStatementAsync(
                new PaginationRequest { PageNumber = 1, PageSize = 20 },
                new CashboxStatementFilterRequest(
                    CashboxId: 1,
                    Classification: CashMovementClassification.Expense));

        Assert.True(cashboxStatement.IsSuccess);
        Assert.Contains(
            cashboxStatement.Value.Items,
            item => item.CashVoucherId == directExpense.Value.Id);
        Assert.Contains(
            cashboxStatement.Value.Items,
            item => item.CashVoucherId == movementExpense.Value.Id);

        var draftsOnly = await service.GetAllAsync(
            new PaginationRequest { PageNumber = 1, PageSize = 20 },
            new CashVoucherFilterRequest(
                Classification: CashMovementClassification.Expense,
                IsDraft: true));

        Assert.True(draftsOnly.IsSuccess);
        var draftItem = Assert.Single(draftsOnly.Value.Items);
        Assert.Equal("CLF-901", draftItem.VoucherNumber);
    }

    [Fact]
    public async Task RevenueFilterIncludesDirectAccountVoucher()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var service = database.CreateVoucherService(companyId: 1);

        var directRevenue = await AddPostedVoucherAsync(
            service,
            CashDirection.Receipt,
            movementTypeId: null,
            accountId: 1,
            amount: 50m);
        var directExpense = await AddPostedVoucherAsync(
            service,
            CashDirection.Payment,
            movementTypeId: null,
            accountId: 2,
            amount: 30m);

        Assert.True(directRevenue.IsSuccess);
        Assert.True(directExpense.IsSuccess);

        var result = await service.GetAllAsync(
            new PaginationRequest { PageNumber = 1, PageSize = 20 },
            new CashVoucherFilterRequest(
                Classification: CashMovementClassification.Revenue,
                IsDraft: false));

        Assert.True(result.IsSuccess);
        Assert.Contains(
            result.Value.Items,
            item => item.Id == directRevenue.Value.Id);
        Assert.DoesNotContain(
            result.Value.Items,
            item => item.Id == directExpense.Value.Id);
    }

    private static async Task<Result<CashVoucherResponse>> AddPostedVoucherAsync(
        ICashVoucherService service,
        CashDirection direction,
        int? movementTypeId,
        int accountId,
        decimal amount)
    {
        var draft = await service.AddAsync(
            new CashVoucherRequest(
                VoucherDate: new DateOnly(2026, 7, 27),
                Direction: direction,
                CashboxId: 1,
                Amount: amount,
                Description: "Classification filter test"));
        if (draft.IsFailure)
        {
            return draft;
        }

        return await service.UpdateAsync(
            draft.Value.Id,
            new CashVoucherUpdateRequest(
                VoucherDate: new DateOnly(2026, 7, 27),
                Direction: direction,
                CashboxId: 1,
                CashMovementTypeId: movementTypeId,
                EmployeeId: null,
                BusinessPartnerId: null,
                DriverId: null,
                DriverTripId: null,
                ExternalPartyName: null,
                Amount: amount,
                ReferenceNumber: null,
                Description: "Classification filter test",
                Notes: null,
                RowVersion: draft.Value.RowVersion,
                ExchangeRate: null,
                AccountId: accountId,
                EmployeeMovementType: null));
    }
}
