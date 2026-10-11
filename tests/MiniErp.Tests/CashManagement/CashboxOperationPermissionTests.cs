using Mapster;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Mappings;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.CashboxTransfers;
using MiniErp.Application.Features.Cashboxes;
using MiniErp.Application.Features.CashboxRevaluations;
using MiniErp.Application.Features.MonetaryAccountRevaluations;
using MiniErp.Application.Features.CashVouchers;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure;

namespace MiniErp.Tests.CashManagement;

public sealed class CashboxOperationPermissionTests
{
    static CashboxOperationPermissionTests() => MappingConfiguration.Register(typeof(InfrastructureAssemblyMarker).Assembly);

    [Fact]
    public async Task RestrictedExecute_DoesNotGrantNewCashboxesOrForeignRevaluation()
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        var permission = new RestrictedCashboxAccess(view: [1, 6], execute: [1]);
        var cashboxCount = await database.Context.Cashboxes.CountAsync();
        var created = await database.CreateCashboxService(1, cashboxAccessService: permission).AddAsync(
            new CashboxRequest(Name: "New restricted cashbox", Currency: CurrencyCode.EGP,
                OpeningBalance: 0m, IsActive: true, Notes: null));
        Assert.Equal(ErrorType.Forbidden, created.Error.Type);
        Assert.Equal(cashboxCount, await database.Context.Cashboxes.CountAsync());
        var revaluation = await database.CreateCashboxRevaluationService(1, cashboxAccessService: permission).CreateAsync(
            new CashboxRevaluationRequest(CashboxId: 6, RevaluationDate: new DateOnly(2026, 8, 8), ClosingRate: 60m));
        Assert.Equal(ErrorType.Forbidden, revaluation.Error.Type);
        Assert.Empty(await database.Context.CashboxRevaluations.ToListAsync());
    }

    [Fact]
    public async Task MonetaryCashAccountWithoutParty_CannotBypassCashboxPermissions()
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        var permission = new RestrictedCashboxAccess(view: [1], execute: [1]);
        await database.SeedPostedJournalEntryAsync(
            journalEntryId: 3101, entryNumber: "JE-UNASSIGNED-CASH", entryDate: new DateOnly(2026, 8, 1),
            entryType: JournalEntryType.Automatic, sourceType: null, sourceId: null, sourceNumber: null,
            lines: [new CashManagementTestDatabase.JournalEntryLineSeed(
                AccountId: 100, PartyType: null, PartyId: null, Debit: 5000m, Credit: 0m,
                Currency: CurrencyCode.EUR, ExchangeRate: 50m, TransactionDebit: 100m, TransactionCredit: 0m)]);
        var service = database.CreateMonetaryAccountRevaluationService(1, cashboxAccessService: permission);
        var options = await service.GetOptionsAsync();
        Assert.True(options.IsSuccess);
        Assert.DoesNotContain(options.Value, row => row.AccountId == 100);
        var created = await service.CreateAsync(new MonetaryAccountRevaluationRequest(
            AccountId: 100, Currency: CurrencyCode.EUR, PartyType: null, PartyId: null,
            RevaluationDate: new DateOnly(2026, 8, 8), ClosingRate: 60m));
        Assert.Equal(ErrorType.Forbidden, created.Error.Type);
        Assert.Empty(await database.Context.MonetaryAccountRevaluations.ToListAsync());
    }

    [Fact]
    public async Task CashboxListsAndOpeningRows_CountOnlyVisibleCashboxes()
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        var permission = new RestrictedCashboxAccess(view: [1], execute: []);
        var cashboxes = database.CreateCashboxService(1, cashboxAccessService: permission);
        var list = await cashboxes.GetAllAsync(new PaginationRequest());
        Assert.True(list.IsSuccess);
        Assert.Equal(1, list.Value.TotalCount);
        Assert.Equal(1, Assert.Single(list.Value.Items).Id);
        Assert.Equal(1000m, list.Value.Items[0].CurrentBalance);
        Assert.Equal(1, Assert.Single((await cashboxes.GetSelectAsync()).Value).Id);
        Assert.Equal(ErrorType.Forbidden, (await cashboxes.GetByIdAsync(2)).Error.Type);

        var vouchers = database.CreateVoucherService(1, cashboxAccessService: permission);
        var page = await vouchers.GetAllAsync(new PaginationRequest());
        Assert.True(page.IsSuccess);
        Assert.Equal(1, page.Value.TotalCount);
        var opening = Assert.Single(page.Value.Items);
        Assert.Equal(1, opening.CashboxId);
        Assert.Equal(1000m, opening.Amount);
        Assert.Equal(ErrorType.Forbidden, (await vouchers.GetAllAsync(new PaginationRequest(), new CashVoucherFilterRequest(CashboxId: 2))).Error.Type);
    }

    [Fact]
    public async Task HandoverCardsAndDraftCounts_ExcludeHiddenCashbox()
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        var unrestricted = database.CreateVoucherService(1);
        Assert.True((await unrestricted.AddAsync(Draft(cashboxId: 1, amount: 25m))).IsSuccess);
        Assert.True((await unrestricted.AddAsync(Draft(cashboxId: 2, amount: 900m))).IsSuccess);
        var restricted = database.CreateVoucherService(1, cashboxAccessService: new RestrictedCashboxAccess(view: [1], execute: []));
        var result = await restricted.GetHandoverReportAsync(new PaginationRequest());
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.TotalCount);
        Assert.Equal(1, Assert.Single(result.Value.Items).CashboxId);
        var card = Assert.Single(result.Value.CashboxBalances);
        Assert.Equal(1, card.CashboxId);
        Assert.Equal(25m, card.DraftReceipt);
        Assert.Equal(0m, card.DraftPayment);
        Assert.Equal(1000m, card.CurrentBalance);
    }

    [Fact]
    public async Task VoucherOldAndNewCashboxesAndBulk_RequireExecuteWithoutSideEffects()
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        var created = await database.CreateVoucherService(1).AddAsync(Draft(cashboxId: 2, amount: 100m));
        Assert.True(created.IsSuccess);
        var originalJournalEntryCount = await database.Context.JournalEntries.CountAsync();
        var service = database.CreateVoucherService(1, cashboxAccessService: new RestrictedCashboxAccess(view: [1, 2], execute: [1]));
        var update = created.Value.Adapt<CashVoucherUpdateRequest>() with
        {
            CashboxId = 1,
            ExternalPartyName = "Visible target",
            RowVersion = created.Value.RowVersion
        };
        Assert.Equal(ErrorType.Forbidden, (await service.UpdateAsync(created.Value.Id, update)).Error.Type);
        Assert.Equal(ErrorType.Forbidden, (await service.DeleteAsync(created.Value.Id)).Error.Type);
        Assert.Equal(ErrorType.Forbidden, (await service.AddAsync(Draft(cashboxId: 2, amount: 1m))).Error.Type);
        var bulk = await service.BulkAsync(new CashVoucherBulkRequest(Items:
            [new CashVoucherBulkDeleteItemRequest(Id: created.Value.Id, RowVersion: created.Value.RowVersion)]));
        Assert.Equal(ErrorType.Forbidden, bulk.Error.Type);
        database.Context.ChangeTracker.Clear();
        var retained = await database.Context.CashVouchers.SingleAsync();
        Assert.Equal(2, retained.CashboxId);
        Assert.Equal(100m, retained.Amount);
        Assert.Equal(originalJournalEntryCount, await database.Context.JournalEntries.CountAsync());

        var visible = await database.CreateVoucherService(1).AddAsync(Draft(cashboxId: 1, amount: 10m));
        Assert.True(visible.IsSuccess);
        var moveToHidden = visible.Value.Adapt<CashVoucherUpdateRequest>() with
        {
            CashboxId = 2, ExternalPartyName = "Hidden target", RowVersion = visible.Value.RowVersion
        };
        Assert.Equal(ErrorType.Forbidden, (await service.UpdateAsync(visible.Value.Id, moveToHidden)).Error.Type);
    }

    [Fact]
    public async Task TransferAndLinkedVoucherReads_RequireBothCashboxesAndMutationsCheckOldPair()
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        var created = await database.CreateCashboxTransferService(1).AddAsync(Transfer());
        Assert.True(created.IsSuccess);
        var permission = new RestrictedCashboxAccess(view: [1], execute: [1]);
        var service = database.CreateCashboxTransferService(1, cashboxAccessService: permission);
        var list = await service.GetAllAsync(new PaginationRequest(), new CashboxTransferFilterRequest());
        Assert.True(list.IsSuccess);
        Assert.Equal(0, list.Value.TotalCount);
        Assert.Equal(ErrorType.Forbidden, (await service.GetByIdAsync(created.Value.Id)).Error.Type);
        var vouchers = database.CreateVoucherService(1, cashboxAccessService: permission);
        Assert.Equal(ErrorType.Forbidden, (await vouchers.GetByIdAsync(created.Value.PaymentVoucherId)).Error.Type);
        var voucherList = await vouchers.GetAllAsync(new PaginationRequest());
        Assert.True(voucherList.IsSuccess);
        var opening = Assert.Single(voucherList.Value.Items);
        Assert.Equal(1, opening.CashboxId);
        Assert.Equal(900m, opening.CashboxBalance);
        Assert.Equal(ErrorType.Forbidden, (await service.AddAsync(Transfer())).Error.Type);
        Assert.Equal(ErrorType.Forbidden, (await service.DeleteAsync(created.Value.Id)).Error.Type);
        var update = created.Value.Adapt<CashboxTransferUpdateRequest>() with { RowVersion = created.Value.RowVersion };
        Assert.Equal(ErrorType.Forbidden, (await service.UpdateAsync(created.Value.Id, update)).Error.Type);
        database.Context.ChangeTracker.Clear();
        Assert.Equal(1, await database.Context.CashboxTransfers.CountAsync());
        Assert.Equal(2, await database.Context.CashVouchers.CountAsync());
    }

    private static CashVoucherRequest Draft(int cashboxId, decimal amount) => new(
        VoucherDate: new DateOnly(2026, 8, 8), Direction: CashDirection.Receipt,
        CashboxId: cashboxId, Amount: amount, Description: "Permission test");

    private static CashboxTransferRequest Transfer() => new(
        TransferDate: new DateOnly(2026, 8, 8), SourceCashboxId: 1,
        DestinationCashboxId: 2, Amount: 100m, Description: "Permission test", Notes: null);
}

internal sealed class RestrictedCashboxAccess(int[] view, int[] execute) : ICashboxAccessService
{
    public Task<Result<CashboxAccessScope>> GetScopeAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Result<CashboxAccessScope>.Success(new CashboxAccessScope(
            HasAllCashboxes: false, ViewCashboxIds: view, ExecuteCashboxIds: execute)));
    public Task<Result> EnsureViewAsync(int cashboxId, CancellationToken cancellationToken = default) =>
        Task.FromResult(view.Contains(cashboxId) ? Result.Success() : Denied());
    public Task<Result> EnsureExecuteAsync(int cashboxId, CancellationToken cancellationToken = default) =>
        Task.FromResult(execute.Contains(cashboxId) ? Result.Success() : Denied());
    private static Result Denied() => Result.Failure(Error.Forbidden("CashboxPermissions.TestDenied", "Denied"));
}
