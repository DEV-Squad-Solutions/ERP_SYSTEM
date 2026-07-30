using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Mappings;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Features.Drivers;
using MiniErp.Domain.Entities.Logistics;
using MiniErp.Infrastructure;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Persistence.Interceptors;
using MiniErp.Infrastructure.Services.Drivers;
using MiniErp.Infrastructure.Services.Pagination;

namespace MiniErp.Tests.Drivers;

public sealed class DriverServiceTests
{
    private static readonly DateTimeOffset CurrentTime =
        new(2026, 7, 26, 12, 0, 0, TimeSpan.Zero);

    static DriverServiceTests()
    {
        MappingConfiguration.Register(
            typeof(InfrastructureAssemblyMarker).Assembly);
    }

    [Fact]
    public async Task GetAll_ReturnsOnlyCurrentCompanyDriversInStableOrder()
    {
        await using var database = await DriverTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);

        var result = await service.GetAllAsync(
            new PaginationRequest
            {
                PageNumber = 1,
                PageSize = 20
            });

        Assert.True(result.IsSuccess);
        Assert.Equal(
            [1, 3, 2, 5],
            result.Value.Items.Select(driver => driver.Id));
    }

    [Fact]
    public async Task GetById_DoesNotReturnAnotherCompanyDriver()
    {
        await using var database = await DriverTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);

        var result = await service.GetByIdAsync(4);

        Assert.True(result.IsFailure);
        Assert.Equal("Drivers.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task GetSelect_ReturnsOnlyActiveUnexpiredCurrentCompanyDrivers()
    {
        await using var database = await DriverTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);

        var result = await service.GetSelectAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal([1, 5], result.Value.Select(driver => driver.Id));
    }

    [Fact]
    public async Task Add_TrimsValuesAndUsesCurrentCompany()
    {
        await using var database = await DriverTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);

        var result = await service.AddAsync(
            new DriverRequest(
                "  NEW  ",
                "  New Driver  ",
                "  01000000000  ",
                "   ",
                "  LIC-NEW  ",
                null));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.CompanyId);
        Assert.Equal("NEW", result.Value.Code);
        Assert.Equal("New Driver", result.Value.Name);
        Assert.Equal("01000000000", result.Value.PhoneNumber);
        Assert.Null(result.Value.NationalId);
        Assert.Equal("LIC-NEW", result.Value.LicenseNumber);
    }

    [Theory]
    [InlineData(
        "Drivers.NameExists",
        "NEW",
        "active driver",
        "LIC-NEW",
        "NAT-NEW")]
    [InlineData(
        "Drivers.CodeExists",
        "drv-1",
        "New Driver",
        "LIC-NEW",
        "NAT-NEW")]
    [InlineData(
        "Drivers.LicenseNumberExists",
        "NEW",
        "New Driver",
        "lic-1",
        "NAT-NEW")]
    [InlineData(
        "Drivers.NationalIdExists",
        "NEW",
        "New Driver",
        "LIC-NEW",
        "nat-1")]
    public async Task Add_RejectsNormalizedCompanyDuplicates(
        string expectedCode,
        string code,
        string name,
        string licenseNumber,
        string nationalId)
    {
        await using var database = await DriverTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);

        var result = await service.AddAsync(
            new DriverRequest(
                code,
                name,
                null,
                nationalId,
                licenseNumber,
                null));

        Assert.True(result.IsFailure);
        Assert.Equal(expectedCode, result.Error.Code);
    }

    [Fact]
    public async Task Update_AllowsTheDriversOwnUniqueValues()
    {
        await using var database = await DriverTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);

        var result = await service.UpdateAsync(
            1,
            new DriverRequest(
                "  DRV-1  ",
                "  Active Driver  ",
                "  01000000001  ",
                "  NAT-1  ",
                "  LIC-1  ",
                null));

        Assert.True(result.IsSuccess);
        Assert.Equal("DRV-1", result.Value.Code);
        Assert.Equal("Active Driver", result.Value.Name);
    }

    [Fact]
    public async Task Update_DoesNotModifyAnotherCompanyDriver()
    {
        await using var database = await DriverTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);

        var result = await service.UpdateAsync(
            4,
            new DriverRequest(
                "CHANGED",
                "Changed Driver",
                null,
                null,
                "LIC-CHANGED",
                null));

        Assert.True(result.IsFailure);
        Assert.Equal("Drivers.NotFound", result.Error.Code);
        Assert.Equal("DRV-4", (await database.GetDriverAsync(4)).Code);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Delete_WhenInvoiceExists_BlocksCurrentAndHistoricalReferences(
        bool isDeleted)
    {
        await using var database = await DriverTestDatabase.CreateAsync();
        await database.AddInvoiceAsync(
            companyId: 1,
            driverId: 2,
            isDeleted: isDeleted);
        var service = database.CreateService(companyId: 1);

        var result = await service.DeleteAsync(2);

        Assert.True(result.IsFailure);
        Assert.Equal("Drivers.HasDependencies", result.Error.Code);
        Assert.False((await database.GetDriverAsync(2)).IsDeleted);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Delete_WhenDriverTripExists_BlocksCurrentAndHistoricalReferences(
        bool isDeleted)
    {
        await using var database = await DriverTestDatabase.CreateAsync();
        await database.AddDriverTripAsync(
            companyId: 1,
            driverId: 2,
            isDeleted: isDeleted);
        var service = database.CreateService(companyId: 1);

        var result = await service.DeleteAsync(2);

        Assert.True(result.IsFailure);
        Assert.Equal("Drivers.HasDependencies", result.Error.Code);
        Assert.False((await database.GetDriverAsync(2)).IsDeleted);
    }

    [Fact]
    public async Task Delete_WhenDriverIsAnInvoiceActualDriver_BlocksDeletion()
    {
        await using var database = await DriverTestDatabase.CreateAsync();
        await database.AddInvoiceAsync(
            companyId: 1,
            driverId: 1,
            isDeleted: false,
            actualDriverId: 2);

        var result = await database.CreateService(companyId: 1).DeleteAsync(2);

        Assert.True(result.IsFailure);
        Assert.Equal("Drivers.HasDependencies", result.Error.Code);
        Assert.False((await database.GetDriverAsync(2)).IsDeleted);
    }

    [Fact]
    public async Task Delete_WhenDriverIsATripActualDriver_BlocksDeletion()
    {
        await using var database = await DriverTestDatabase.CreateAsync();
        await database.AddDriverTripAsync(
            companyId: 1,
            driverId: 1,
            isDeleted: false,
            actualDriverId: 2);

        var result = await database.CreateService(companyId: 1).DeleteAsync(2);

        Assert.True(result.IsFailure);
        Assert.Equal("Drivers.HasDependencies", result.Error.Code);
        Assert.False((await database.GetDriverAsync(2)).IsDeleted);
    }

    [Fact]
    public async Task Delete_WhenDriverIsUnused_SoftDeletesIt()
    {
        await using var database = await DriverTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);

        var result = await service.DeleteAsync(5);

        Assert.True(result.IsSuccess);
        var driver = await database.GetDriverAsync(5);
        Assert.False(driver.IsActive);
        Assert.True(driver.IsDeleted);
    }

    [Fact]
    public async Task Delete_DoesNotDeleteAnotherCompanyDriver()
    {
        await using var database = await DriverTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);

        var result = await service.DeleteAsync(4);

        Assert.True(result.IsFailure);
        Assert.Equal("Drivers.NotFound", result.Error.Code);
        Assert.False((await database.GetDriverAsync(4)).IsDeleted);
    }

    [Fact]
    public async Task Validator_AcceptsValuesWhoseTrimmedLengthsAreValid()
    {
        var validator = new DriverRequestValidator();
        var request = new DriverRequest(
            $"  {new string('C', 50)}  ",
            $"  {new string('N', 200)}  ",
            $"  {new string('P', 50)}  ",
            new string(' ', 100),
            $"  {new string('L', 100)}  ",
            null);

        var result = await validator.ValidateAsync(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validator_UsesSharedMaximumLengthRuleForTrimmedValue()
    {
        var validator = new DriverRequestValidator();
        var request = new DriverRequest(
            $"  {new string('C', 51)}  ",
            "Driver",
            null,
            null,
            "LIC-NEW",
            null);

        var result = await validator.ValidateAsync(request);

        var error = Assert.Single(result.Errors);
        Assert.Equal(nameof(DriverRequest.Code), error.PropertyName);
        Assert.Equal("MaximumLengthValidator", error.ErrorCode);
    }

    [Fact]
    public async Task Validator_ReturnsOneRequiredErrorForEachWhitespaceValue()
    {
        var validator = new DriverRequestValidator();
        var request = new DriverRequest(
            "   ",
            "   ",
            null,
            null,
            "   ",
            null);

        var result = await validator.ValidateAsync(request);

        Assert.Equal(3, result.Errors.Count);
        Assert.All(
            result.Errors,
            error => Assert.Equal("NotEmptyValidator", error.ErrorCode));
    }

    private sealed class DriverTestDatabase : IAsyncDisposable
    {
        private DriverTestDatabase(
            SqliteConnection connection,
            ApplicationDbContext context,
            TimeProvider timeProvider)
        {
            Connection = connection;
            Context = context;
            TimeProvider = timeProvider;
        }

        private SqliteConnection Connection { get; }

        private ApplicationDbContext Context { get; }

        private TimeProvider TimeProvider { get; }

        public static async Task<DriverTestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var timeProvider = new FixedTimeProvider(CurrentTime);
            var auditInterceptor = new AuditableEntityInterceptor(
                new HttpContextAccessor(),
                timeProvider);
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .AddInterceptors(auditInterceptor)
                .Options;
            var context = new ApplicationDbContext(options);

            await CreateSchemaAsync(context);
            await SeedAsync(context);

            return new DriverTestDatabase(
                connection,
                context,
                timeProvider);
        }

        public DriverService CreateService(int companyId) =>
            new(
                Context,
                new PaginationService(),
                new TestCurrentCompanyContext(companyId),
                TimeProvider);

        public Task AddInvoiceAsync(
            int companyId,
            int driverId,
            bool isDeleted,
            int? actualDriverId = null) =>
            Context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 INSERT INTO Invoices (
                     Id, CompanyId, DriverId, ActualDriverId, IsDeleted)
                 VALUES (
                     100, {companyId}, {driverId}, {actualDriverId}, {isDeleted})
                 """);

        public Task AddDriverTripAsync(
            int companyId,
            int driverId,
            bool isDeleted,
            int? actualDriverId = null) =>
            Context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 INSERT INTO DriverTrips (
                     Id, CompanyId, DriverId, ActualDriverId, IsDeleted)
                 VALUES (
                     100, {companyId}, {driverId}, {actualDriverId}, {isDeleted})
                 """);

        public async Task<Driver> GetDriverAsync(int driverId)
        {
            Context.ChangeTracker.Clear();

            return await Context.Drivers
                .IgnoreQueryFilters()
                .AsNoTracking()
                .SingleAsync(driver => driver.Id == driverId);
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
                CREATE TABLE Drivers (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    Code TEXT COLLATE NOCASE NOT NULL,
                    Name TEXT COLLATE NOCASE NOT NULL,
                    PhoneNumber TEXT NULL,
                    NationalId TEXT COLLATE NOCASE NULL,
                    LicenseNumber TEXT COLLATE NOCASE NOT NULL,
                    LicenseExpiryDate TEXT NULL,
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

                CREATE UNIQUE INDEX UX_Drivers_CompanyId_Code
                ON Drivers (CompanyId, Code)
                WHERE IsDeleted = 0;

                CREATE UNIQUE INDEX UX_Drivers_CompanyId_Name
                ON Drivers (CompanyId, Name)
                WHERE IsDeleted = 0;

                CREATE UNIQUE INDEX UX_Drivers_CompanyId_LicenseNumber
                ON Drivers (CompanyId, LicenseNumber)
                WHERE IsDeleted = 0;

                CREATE UNIQUE INDEX UX_Drivers_CompanyId_NationalId
                ON Drivers (CompanyId, NationalId)
                WHERE NationalId IS NOT NULL AND IsDeleted = 0;

                CREATE TABLE Invoices (
                    Id INTEGER PRIMARY KEY,
                    CompanyId INTEGER NOT NULL,
                    ContentType INTEGER NOT NULL DEFAULT 1,
                    DriverId INTEGER NULL,
                    ActualDriverId INTEGER NULL,
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TABLE DriverTrips (
                    Id INTEGER PRIMARY KEY,
                    CompanyId INTEGER NOT NULL,
                    DriverId INTEGER NOT NULL,
                    ActualDriverId INTEGER NULL,
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TABLE CashVouchers (
                    Id INTEGER PRIMARY KEY,
                    CompanyId INTEGER NOT NULL,
                    DriverId INTEGER NULL,
                    IsDeleted INTEGER NOT NULL
                );
                """);
        }

        private static async Task SeedAsync(ApplicationDbContext context)
        {
            context.Drivers.AddRange(
                CreateDriver(
                    id: 1,
                    companyId: 1,
                    code: "DRV-1",
                    name: "Active Driver",
                    nationalId: "NAT-1",
                    licenseNumber: "LIC-1",
                    licenseExpiryDate: null,
                    isActive: true),
                CreateDriver(
                    id: 2,
                    companyId: 1,
                    code: "DRV-2",
                    name: "Inactive Driver",
                    nationalId: "NAT-2",
                    licenseNumber: "LIC-2",
                    licenseExpiryDate: new DateOnly(2027, 1, 1),
                    isActive: false),
                CreateDriver(
                    id: 3,
                    companyId: 1,
                    code: "DRV-3",
                    name: "Expired Driver",
                    nationalId: "NAT-3",
                    licenseNumber: "LIC-3",
                    licenseExpiryDate: new DateOnly(2026, 7, 25),
                    isActive: true),
                CreateDriver(
                    id: 4,
                    companyId: 2,
                    code: "DRV-4",
                    name: "Other Company Driver",
                    nationalId: "NAT-4",
                    licenseNumber: "LIC-4",
                    licenseExpiryDate: new DateOnly(2027, 1, 1),
                    isActive: true),
                CreateDriver(
                    id: 5,
                    companyId: 1,
                    code: "DRV-5",
                    name: "Unused Driver",
                    nationalId: null,
                    licenseNumber: "LIC-5",
                    licenseExpiryDate: new DateOnly(2026, 7, 26),
                    isActive: true));
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();
        }

        private static Driver CreateDriver(
            int id,
            int companyId,
            string code,
            string name,
            string? nationalId,
            string licenseNumber,
            DateOnly? licenseExpiryDate,
            bool isActive) =>
            new()
            {
                Id = id,
                CompanyId = companyId,
                Code = code,
                Name = name,
                NationalId = nationalId,
                LicenseNumber = licenseNumber,
                LicenseExpiryDate = licenseExpiryDate,
                IsActive = isActive
            };
    }

    private sealed record TestCurrentCompanyContext(int CompanyId)
        : ICurrentCompanyContext;

    private sealed class FixedTimeProvider(DateTimeOffset currentTime)
        : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => currentTime;
    }
}
