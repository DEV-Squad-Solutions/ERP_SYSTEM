using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Authentication;
using MiniErp.Application.Common.Realtime;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.Users;
using MiniErp.Infrastructure.Identity;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Services.Users;

namespace MiniErp.Tests.Users;

public sealed class CashboxPermissionServiceTests
{
    [Fact]
    public async Task NoGrants_DeniesViewAndExecute()
    {
        await using var fixture = await Fixture.CreateAsync();
        var access = fixture.Access(fixture.UserId);
        var scope = await access.GetScopeAsync();
        Assert.True(scope.IsSuccess);
        Assert.False(scope.Value.HasAllCashboxes);
        Assert.Empty(scope.Value.ViewCashboxIds);
        Assert.Empty(scope.Value.ExecuteCashboxIds);
        Assert.Equal(ErrorType.Forbidden, (await access.EnsureViewAsync(1)).Error.Type);
        Assert.Equal(ErrorType.Forbidden, (await access.EnsureExecuteAsync(1)).Error.Type);
    }

    [Fact]
    public async Task ViewAndExecute_AreIndependent_AndExecuteIncludesView()
    {
        await using var fixture = await Fixture.CreateAsync();
        var saved = await fixture.Management().ReplaceAsync(fixture.UserId, new UserCashboxPermissionsRequest(
            AllCashboxes: false, Revision: Guid.Empty,
            Permissions: [new(CashboxId: 1, CanView: true, CanExecute: false), new(CashboxId: 2, CanView: false, CanExecute: true)]));
        Assert.True(saved.IsSuccess);
        Assert.Equal(fixture.UserId, saved.Value.UserId);
        Assert.Equal(1, saved.Value.CompanyId);
        Assert.False(saved.Value.AllCashboxes);
        Assert.NotEqual(Guid.Empty, saved.Value.Revision);
        var executionGrant = saved.Value.Permissions.Single(grant => grant.CashboxId == 2);
        Assert.True(executionGrant.CanView);
        Assert.True(executionGrant.CanExecute);
        var access = fixture.Access(fixture.UserId);
        var scope = (await access.GetScopeAsync()).Value;
        Assert.Equal([1, 2], scope.ViewCashboxIds.Order());
        Assert.Equal([2], scope.ExecuteCashboxIds);
        Assert.True((await access.EnsureViewAsync(1)).IsSuccess);
        Assert.True((await access.EnsureExecuteAsync(1)).IsFailure);
        Assert.True((await access.EnsureExecuteAsync(2)).IsSuccess);
    }

    [Fact]
    public async Task AllCashboxesAndAdmin_StillRequireCompanyMembership_AndCorrectCashboxCompany()
    {
        await using var fixture = await Fixture.CreateAsync();
        var saved = await fixture.Management().ReplaceAsync(fixture.UserId,
            new UserCashboxPermissionsRequest(AllCashboxes: true, Revision: Guid.Empty, Permissions: []));
        Assert.True(saved.IsSuccess);
        Assert.True((await fixture.Access(fixture.UserId).GetScopeAsync()).Value.HasAllCashboxes);
        Assert.True((await fixture.Access(fixture.UserId).EnsureExecuteAsync(2)).IsSuccess);
        Assert.True((await fixture.Access(fixture.UserId).EnsureViewAsync(3)).IsFailure);
        Assert.True((await fixture.Access(fixture.AdminId).GetScopeAsync()).Value.HasAllCashboxes);
        Assert.True((await fixture.Access(fixture.AdminId, companyId: 2).GetScopeAsync()).IsFailure);
        Assert.True((await fixture.Access(fixture.AdminId).EnsureExecuteAsync(3)).IsFailure);
    }

    [Fact]
    public async Task Replace_RevokesImmediately_AndRejectsStaleRevision()
    {
        await using var fixture = await Fixture.CreateAsync();
        var service = fixture.Management();
        var saved = await service.ReplaceAsync(fixture.UserId,
            new UserCashboxPermissionsRequest(AllCashboxes: true, Revision: Guid.Empty, Permissions: []));
        var access = fixture.Access(fixture.UserId);
        Assert.True((await access.GetScopeAsync()).Value.HasAllCashboxes);
        var revoked = await service.ReplaceAsync(fixture.UserId,
            new UserCashboxPermissionsRequest(AllCashboxes: false, Revision: saved.Value.Revision, Permissions: []));
        Assert.True(revoked.IsSuccess);
        Assert.True((await access.EnsureViewAsync(1)).IsFailure);
        var stale = await service.ReplaceAsync(fixture.UserId,
            new UserCashboxPermissionsRequest(AllCashboxes: true, Revision: saved.Value.Revision, Permissions: []));
        Assert.Equal(ErrorType.Conflict, stale.Error.Type);
        Assert.False((await access.GetScopeAsync()).Value.HasAllCashboxes);
    }

    [Fact]
    public async Task Management_RejectsCrossCompanyGrant_AndNonAdmin()
    {
        await using var fixture = await Fixture.CreateAsync();
        var result = await fixture.Management().ReplaceAsync(fixture.UserId,
            new UserCashboxPermissionsRequest(AllCashboxes: false, Revision: Guid.Empty,
                Permissions: [new(CashboxId: 3, CanView: true, CanExecute: true)]));
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Empty(await fixture.Context.UserCashboxPermissions.ToListAsync());
        Assert.Equal(ErrorType.Forbidden, (await fixture.Management(fixture.UserId).GetAsync(fixture.UserId)).Error.Type);
        Assert.Equal(ErrorType.Forbidden, (await fixture.Management(companyId: 2).GetAsync(fixture.UserId)).Error.Type);
    }

    [Fact]
    public async Task DatabaseForeignKey_RejectsCashboxFromDifferentCompany()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Context.UserCashboxPermissions.Add(new UserCashboxPermission
        {
            UserId = fixture.UserId, CompanyId = 1, CashboxId = 3, CanView = true, CanExecute = false
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => fixture.Context.SaveChangesAsync());
    }

    [Fact]
    public async Task DuplicateGrants_MergeFlags_AndCompanyRemovalCascades()
    {
        await using var fixture = await Fixture.CreateAsync();
        var saved = await fixture.Management().ReplaceAsync(fixture.UserId,
            new UserCashboxPermissionsRequest(AllCashboxes: false, Revision: Guid.Empty,
                Permissions: [new(CashboxId: 1, CanView: true, CanExecute: false), new(CashboxId: 1, CanView: false, CanExecute: true)]));
        Assert.True(saved.IsSuccess);
        Assert.True(Assert.Single(saved.Value.Permissions).CanExecute);
        var membership = await fixture.Context.UserCompanies.SingleAsync(mapping => mapping.UserId == fixture.UserId);
        fixture.Context.UserCompanies.Remove(membership);
        await fixture.Context.SaveChangesAsync();
        Assert.Empty(await fixture.Context.UserCashboxPermissions.IgnoreQueryFilters().ToListAsync());
        Assert.True((await fixture.Access(fixture.UserId).GetScopeAsync()).IsFailure);
    }

    [Fact]
    public async Task AdminRoleRemoval_IsObservedByTheSameResolverInstance()
    {
        await using var fixture = await Fixture.CreateAsync();
        var access = fixture.Access(fixture.AdminId);
        Assert.True((await access.GetScopeAsync()).Value.HasAllCashboxes);
        var role = await fixture.Context.UserRoles.SingleAsync(mapping => mapping.UserId == fixture.AdminId);
        fixture.Context.UserRoles.Remove(role);
        await fixture.Context.SaveChangesAsync();
        Assert.False((await access.GetScopeAsync()).Value.HasAllCashboxes);
        Assert.True((await access.EnsureExecuteAsync(1)).IsFailure);
        Assert.Equal(ErrorType.Forbidden, (await fixture.Management().GetAsync(fixture.UserId)).Error.Type);
    }

    [Fact]
    public async Task InactiveCashbox_CanRetainHistoricalGrant_ButCannotReceiveNewGrant()
    {
        await using var fixture = await Fixture.CreateAsync();
        var service = fixture.Management();
        var saved = await service.ReplaceAsync(fixture.UserId,
            new UserCashboxPermissionsRequest(AllCashboxes: false, Revision: Guid.Empty,
                Permissions: [new(CashboxId: 1, CanView: true, CanExecute: false)]));
        await fixture.Context.Database.ExecuteSqlRawAsync("UPDATE Cashboxes SET IsActive = 0 WHERE CompanyId = 1");
        var retained = await service.ReplaceAsync(fixture.UserId,
            new UserCashboxPermissionsRequest(AllCashboxes: false, Revision: saved.Value.Revision,
                Permissions: [new(CashboxId: 1, CanView: true, CanExecute: false)]));
        Assert.True(retained.IsSuccess);
        Assert.True((await fixture.Access(fixture.UserId).EnsureViewAsync(1)).IsSuccess);
        var newGrant = await service.ReplaceAsync(fixture.UserId,
            new UserCashboxPermissionsRequest(AllCashboxes: false, Revision: retained.Value.Revision,
                Permissions: [new(CashboxId: 2, CanView: true, CanExecute: false)]));
        Assert.Equal(ErrorType.Validation, newGrant.Error.Type);
        Assert.Equal(1, Assert.Single((await service.GetAsync(fixture.UserId)).Value.Permissions).CashboxId);
    }

    [Fact]
    public async Task RealtimeAudience_UsesLiveViewGrants_AndRevocation()
    {
        await using var fixture = await Fixture.CreateAsync();
        var audience = new CashboxRealtimeAudience(fixture.Context);
        var first = CashboxEvent(cashboxId: 1, companyId: 1);
        Assert.Equal([fixture.AdminId], await audience.ResolveAsync("Cashbox", first));
        var service = fixture.Management();
        var saved = await service.ReplaceAsync(fixture.UserId,
            new UserCashboxPermissionsRequest(AllCashboxes: false, Revision: Guid.Empty,
                Permissions: [new(CashboxId: 1, CanView: true, CanExecute: false)]));
        Assert.True(saved.IsSuccess);
        Assert.Contains(fixture.UserId, await audience.ResolveAsync("Cashbox", first));
        Assert.DoesNotContain(fixture.UserId, await audience.ResolveAsync("Cashbox", CashboxEvent(cashboxId: 2, companyId: 1)));
        var revoked = await service.ReplaceAsync(fixture.UserId,
            new UserCashboxPermissionsRequest(AllCashboxes: false, Revision: saved.Value.Revision, Permissions: []));
        Assert.True(revoked.IsSuccess);
        Assert.DoesNotContain(fixture.UserId, await audience.ResolveAsync("Cashbox", first));
    }

    [Fact]
    public async Task RealtimeAudience_AdminAndAllCashboxes_RequireCompanyMembership_AndIncludeFutureCashboxes()
    {
        await using var fixture = await Fixture.CreateAsync();
        var audience = new CashboxRealtimeAudience(fixture.Context);
        Assert.Empty(await audience.ResolveAsync("Cashbox", CashboxEvent(cashboxId: 3, companyId: 2)));
        var saved = await fixture.Management().ReplaceAsync(fixture.UserId,
            new UserCashboxPermissionsRequest(AllCashboxes: true, Revision: Guid.Empty, Permissions: []));
        Assert.True(saved.IsSuccess);
        await fixture.Context.Database.ExecuteSqlRawAsync("""
            INSERT INTO Cashboxes (Id, CompanyId, Code, Name, Currency, OpeningBalance, OpeningBalanceDate, RowVersion, CreatedById, CreatedOn, CreatedByPc, IsDeleted)
            VALUES (4, 1, 'D', 'D', 1, 0, '2026-01-01', X'04', 'test', '2026-01-01', 'test', 0);
            """);
        var recipients = await audience.ResolveAsync("Cashbox", CashboxEvent(cashboxId: 4, companyId: 1));
        Assert.Contains(fixture.AdminId, recipients);
        Assert.Contains(fixture.UserId, recipients);
        Assert.Equal(2, recipients.Length);
        Assert.Empty(await audience.ResolveAsync("Cashbox", CashboxEvent(cashboxId: 4, companyId: 2)));
    }

    private static RealtimeJobRequest CashboxEvent(int cashboxId, int companyId) => new(
        OperationId: Guid.NewGuid(), Action: "Updated", EntityId: cashboxId.ToString(), ActorUserId: null, CompanyId: companyId);

    private sealed class Fixture(SqliteConnection connection, ApplicationDbContext context) : IAsyncDisposable
    {
        public ApplicationDbContext Context => context;
        public Guid AdminId { get; } = Guid.NewGuid();
        public Guid UserId { get; } = Guid.NewGuid();

        public CashboxAccessService Access(Guid userId, int companyId = 1) => new(context, new CompanyContext(companyId), new UserContext(userId));
        public UserCashboxPermissionService Management(Guid? actor = null, int companyId = 1) => new(
            context, new CompanyContext(companyId), new UserContext(actor ?? AdminId), NullLogger<UserCashboxPermissionService>.Instance);

        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await context.Database.EnsureCreatedAsync();
            var fixture = new Fixture(connection, context);
            await context.Database.ExecuteSqlRawAsync("""
                INSERT INTO Companies (Id, Name, Address, CommercialRegister, TaxNumber, ManagerName, RowVersion, CreatedById, CreatedOn, CreatedByPc, IsDeleted)
                VALUES (1, 'One', '', 'CR-1', 'TAX-1', '', X'01', 'test', '2026-01-01', 'test', 0),
                       (2, 'Two', '', 'CR-2', 'TAX-2', '', X'02', 'test', '2026-01-01', 'test', 0);
                INSERT INTO Cashboxes (Id, CompanyId, Code, Name, Currency, OpeningBalance, OpeningBalanceDate, RowVersion, CreatedById, CreatedOn, CreatedByPc, IsDeleted)
                VALUES (1, 1, 'A', 'A', 1, 0, '2026-01-01', X'01', 'test', '2026-01-01', 'test', 0),
                       (2, 1, 'B', 'B', 1, 0, '2026-01-01', X'02', 'test', '2026-01-01', 'test', 0),
                       (3, 2, 'C', 'C', 1, 0, '2026-01-01', X'03', 'test', '2026-01-01', 'test', 0);
                """);
            var adminRole = new IdentityRole<Guid>(ApplicationRoles.Admin) { Id = Guid.NewGuid() };
            context.Roles.Add(adminRole);
            context.Users.AddRange(new ApplicationUser { Id = fixture.AdminId }, new ApplicationUser { Id = fixture.UserId });
            context.UserRoles.Add(new IdentityUserRole<Guid> { UserId = fixture.AdminId, RoleId = adminRole.Id });
            context.UserCompanies.AddRange(new UserCompany { UserId = fixture.AdminId, CompanyId = 1 }, new UserCompany { UserId = fixture.UserId, CompanyId = 1 });
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();
            return fixture;
        }

        public async ValueTask DisposeAsync()
        {
            await context.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed class CompanyContext(int companyId) : ICurrentCompanyContext { public int CompanyId => companyId; }
    private sealed class UserContext(Guid userId) : ICurrentUserService { public Result<Guid> GetUserId() => Result<Guid>.Success(userId); }
}
