using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Mappings;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Features.Containers;
using MiniErp.Domain.Entities.Containers;
using MiniErp.Infrastructure;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Persistence.Interceptors;
using MiniErp.Infrastructure.Services.Containers;
using MiniErp.Infrastructure.Services.Pagination;

namespace MiniErp.Tests.Containers;

public sealed class ContainerServiceTests
{
    static ContainerServiceTests()
    {
        MappingConfiguration.Register(
            typeof(InfrastructureAssemblyMarker).Assembly);
    }

    [Fact]
    public async Task GetAll_ReturnsCurrentCompanyActiveAndInactiveContainers()
    {
        await using var database = await ContainerTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);

        var result = await service.GetAllAsync(
            new PaginationRequest
            {
                PageNumber = 1,
                PageSize = 20
            });

        Assert.True(result.IsSuccess);
        Assert.Equal([1, 2], result.Value.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task GetById_DoesNotReturnAnotherCompanyContainer()
    {
        await using var database = await ContainerTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);

        var result = await service.GetByIdAsync(3);

        Assert.True(result.IsFailure);
        Assert.Equal("Containers.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task GetSelect_ReturnsOnlyActiveCurrentCompanyContainers()
    {
        await using var database = await ContainerTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);

        var result = await service.GetSelectAsync();

        Assert.True(result.IsSuccess);
        var container = Assert.Single(result.Value);
        Assert.Equal(1, container.Id);
        Assert.Equal("Active Container", container.Name);
    }

    [Fact]
    public async Task Add_TrimsValuesAndUsesCurrentCompany()
    {
        await using var database = await ContainerTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);

        var result = await service.AddAsync(
            new ContainerRequest(
                "  NEW  ",
                "  New Container  ",
                "  Description  "));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.CompanyId);
        Assert.Equal("NEW", result.Value.Code);
        Assert.Equal("New Container", result.Value.Name);
        Assert.Equal("Description", result.Value.Description);
    }

    [Fact]
    public async Task Add_RejectsDuplicateActiveCode()
    {
        await using var database = await ContainerTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);

        var result = await service.AddAsync(
            new ContainerRequest(
                "  BOX  ",
                "Another Container",
                null));

        Assert.True(result.IsFailure);
        Assert.Equal("Containers.CodeExists", result.Error.Code);
    }

    [Fact]
    public async Task InactiveDuplicateCanBeCreatedButCannotBeReactivated()
    {
        await using var database = await ContainerTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);

        var addResult = await service.AddAsync(
            new ContainerRequest(
                "BOX",
                "Inactive Duplicate",
                null,
                IsActive: false));

        Assert.True(addResult.IsSuccess);

        var updateResult = await service.UpdateAsync(
            addResult.Value.Id,
            new ContainerRequest(
                "BOX",
                "Inactive Duplicate",
                null,
                IsActive: true));

        Assert.True(updateResult.IsFailure);
        Assert.Equal("Containers.CodeExists", updateResult.Error.Code);
    }

    [Fact]
    public async Task Delete_WhenHistoricalStoreAssignmentExists_ReturnsConflict()
    {
        await using var database = await ContainerTestDatabase.CreateAsync();
        await database.AddHistoricalAssignmentAsync(containerId: 2);
        var service = database.CreateService(companyId: 1);

        var result = await service.DeleteAsync(2);

        Assert.True(result.IsFailure);
        Assert.Equal("Containers.HasStoreAssignments", result.Error.Code);
        Assert.False((await database.GetContainerAsync(2)).IsDeleted);
    }

    [Fact]
    public async Task Delete_WhenContainerIsUnused_SoftDeletesIt()
    {
        await using var database = await ContainerTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);

        var result = await service.DeleteAsync(2);

        Assert.True(result.IsSuccess);
        var container = await database.GetContainerAsync(2);
        Assert.False(container.IsActive);
        Assert.True(container.IsDeleted);
    }

    [Fact]
    public async Task Validator_AcceptsValuesWhoseTrimmedLengthsAreValid()
    {
        var validator = new ContainerRequestValidator();
        var request = new ContainerRequest(
            $"  {new string('C', 50)}  ",
            $"  {new string('N', 200)}  ",
            $"  {new string('D', 1_000)}  ");

        var result = await validator.ValidateAsync(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validator_UsesSharedMaximumLengthRuleForTrimmedValue()
    {
        var validator = new ContainerRequestValidator();
        var request = new ContainerRequest(
            $"  {new string('C', 51)}  ",
            "Container",
            null);

        var result = await validator.ValidateAsync(request);

        var error = Assert.Single(result.Errors);
        Assert.Equal(nameof(ContainerRequest.Code), error.PropertyName);
        Assert.Equal("MaximumLengthValidator", error.ErrorCode);
    }

    [Fact]
    public async Task Validator_ReturnsOneRequiredErrorForWhitespaceCode()
    {
        var validator = new ContainerRequestValidator();
        var request = new ContainerRequest(
            "   ",
            "Container",
            null);

        var result = await validator.ValidateAsync(request);

        var error = Assert.Single(result.Errors);
        Assert.Equal(nameof(ContainerRequest.Code), error.PropertyName);
        Assert.Equal("NotEmptyValidator", error.ErrorCode);
    }

    private sealed class ContainerTestDatabase : IAsyncDisposable
    {
        private ContainerTestDatabase(
            SqliteConnection connection,
            ApplicationDbContext context)
        {
            Connection = connection;
            Context = context;
        }

        private SqliteConnection Connection { get; }

        private ApplicationDbContext Context { get; }

        public static async Task<ContainerTestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var auditInterceptor = new AuditableEntityInterceptor(
                new HttpContextAccessor(),
                TimeProvider.System);
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .AddInterceptors(auditInterceptor)
                .Options;
            var context = new ApplicationDbContext(options);

            await CreateSchemaAsync(context);
            await SeedAsync(context);

            return new ContainerTestDatabase(connection, context);
        }

        public ContainerService CreateService(int companyId) =>
            new(
                Context,
                new PaginationService(),
                new TestCurrentCompanyContext(companyId));

        public Task AddHistoricalAssignmentAsync(int containerId) =>
            Context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 INSERT INTO StoreContainers (
                     Id, CompanyId, StoreId, ContainerId, IsDeleted)
                 VALUES (100, 1, 10, {containerId}, 1)
                 """);

        public async Task<Container> GetContainerAsync(int containerId)
        {
            Context.ChangeTracker.Clear();

            return await Context.Containers
                .IgnoreQueryFilters()
                .AsNoTracking()
                .SingleAsync(container => container.Id == containerId);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await Connection.DisposeAsync();
        }

        private static async Task CreateSchemaAsync(
            ApplicationDbContext context)
        {
            await context.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE Containers (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    Code TEXT NOT NULL,
                    Name TEXT NOT NULL,
                    Description TEXT NULL,
                    IsActive INTEGER NOT NULL,
                    CreatedById TEXT NOT NULL,
                    CreatedOn TEXT NOT NULL,
                    CreatedByPc TEXT NOT NULL,
                    UpdatedById TEXT NULL,
                    UpdatedOn TEXT NULL,
                    UpdatedByPc TEXT NULL,
                    DeletedById TEXT NULL,
                    DeletedOn TEXT NULL,
                    DeletedByPc TEXT NULL,
                    IsDeleted INTEGER NOT NULL
                );

                CREATE UNIQUE INDEX UX_Containers_CompanyId_Code_Active
                ON Containers (CompanyId, Code)
                WHERE IsActive = 1 AND IsDeleted = 0;

                CREATE TABLE StoreContainers (
                    Id INTEGER PRIMARY KEY,
                    CompanyId INTEGER NOT NULL,
                    StoreId INTEGER NOT NULL,
                    ContainerId INTEGER NOT NULL,
                    IsDeleted INTEGER NOT NULL
                );
                """);
        }

        private static async Task SeedAsync(ApplicationDbContext context)
        {
            context.Containers.AddRange(
                CreateContainer(
                    id: 1,
                    companyId: 1,
                    code: "BOX",
                    name: "Active Container",
                    isActive: true),
                CreateContainer(
                    id: 2,
                    companyId: 1,
                    code: "OLD",
                    name: "Inactive Container",
                    isActive: false),
                CreateContainer(
                    id: 3,
                    companyId: 2,
                    code: "OTHER",
                    name: "Other Company Container",
                    isActive: true));
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();
        }

        private static Container CreateContainer(
            int id,
            int companyId,
            string code,
            string name,
            bool isActive) =>
            new()
            {
                Id = id,
                CompanyId = companyId,
                Code = code,
                Name = name,
                IsActive = isActive
            };
    }

    private sealed record TestCurrentCompanyContext(int CompanyId)
        : ICurrentCompanyContext;
}
