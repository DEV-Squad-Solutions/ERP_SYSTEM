using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Authentication;
using MiniErp.Application.Common.Mappings;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.Users;
using MiniErp.Infrastructure;
using MiniErp.Infrastructure.Identity;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Services.Users;

namespace MiniErp.Tests.Users;

public sealed class UserServiceTests
{
    private static readonly Guid AdminId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static readonly Guid UserId =
        Guid.Parse("22222222-2222-2222-2222-222222222222");

    static UserServiceTests()
    {
        MappingConfiguration.Register(
            typeof(InfrastructureAssemblyMarker).Assembly);
    }

    [Fact]
    public async Task Add_CreatesNormalizedUserWithRolesAndCompanies()
    {
        await using var database = await UserTestDatabase.CreateAsync();
        var service = database.CreateService(AdminId);

        var result = await service.AddAsync(
            new UserCreateRequest(
                "  new-user  ",
                "  new-user@example.com  ",
                "  New  ",
                "  User  ",
                "  01000000000  ",
                "P@ssword123",
                [ApplicationRoles.User],
                [1, 2]));

        Assert.True(result.IsSuccess);
        Assert.Equal("new-user", result.Value.UserName);
        Assert.Equal("new-user@example.com", result.Value.Email);
        Assert.Equal("New", result.Value.FirstName);
        Assert.Equal("User", result.Value.LastName);
        Assert.Equal("01000000000", result.Value.PhoneNumber);
        Assert.Equal([ApplicationRoles.User], result.Value.Roles);
        Assert.Equal([1, 2], result.Value.Companies.Select(company => company.Id));

        var user = await database.UserManager.FindByIdAsync(
            result.Value.Id.ToString());
        Assert.NotNull(user);
        Assert.True(await database.UserManager.IsInRoleAsync(
            user,
            ApplicationRoles.User));
    }

    [Fact]
    public async Task Add_WhenRoleAssignmentFails_RollsBackTheUser()
    {
        await using var database = await UserTestDatabase.CreateAsync();
        var service = database.CreateService(
            AdminId,
            database.CreateFailingRoleUserManager());

        var result = await service.AddAsync(
            new UserCreateRequest(
                "rollback-user",
                "rollback-user@example.com",
                "Rollback",
                "User",
                null,
                "P@ssword123",
                [ApplicationRoles.User],
                [1]));

        Assert.True(result.IsFailure);
        database.Context.ChangeTracker.Clear();
        Assert.False(await database.Context.Users.AnyAsync(user =>
            user.NormalizedUserName == "ROLLBACK-USER"));
    }

    [Fact]
    public async Task Update_WhenRolesChange_RotatesStampAndReplacesAssignments()
    {
        await using var database = await UserTestDatabase.CreateAsync();
        var service = database.CreateService(AdminId);
        var originalStamp = await database.GetSecurityStampAsync(UserId);

        var result = await service.UpdateAsync(
            UserId,
            new UserUpdateRequest(
                "  updated-user  ",
                "  updated-user@example.com  ",
                "  Updated  ",
                "  User  ",
                null,
                [ApplicationRoles.Admin],
                [2]));

        Assert.True(result.IsSuccess);
        Assert.Equal([ApplicationRoles.Admin], result.Value.Roles);
        Assert.Equal([2], result.Value.Companies.Select(company => company.Id));
        Assert.NotEqual(
            originalStamp,
            await database.GetSecurityStampAsync(UserId));

        var user = await database.UserManager.FindByIdAsync(UserId.ToString());
        Assert.NotNull(user);
        var roles = await database.UserManager.GetRolesAsync(user);
        var companyIds = await database.GetCompanyIdsAsync(UserId);
        Assert.Equal(
            [ApplicationRoles.Admin],
            roles);
        Assert.Equal([2], companyIds);
    }

    [Fact]
    public async Task Update_WhenRolesDoNotChange_PreservesSecurityStamp()
    {
        await using var database = await UserTestDatabase.CreateAsync();
        var service = database.CreateService(AdminId);
        var originalStamp = await database.GetSecurityStampAsync(UserId);

        var result = await service.UpdateAsync(
            UserId,
            new UserUpdateRequest(
                "standard-user",
                "standard-user@example.com",
                "Changed",
                "Name",
                null,
                [ApplicationRoles.User],
                [1]));

        Assert.True(result.IsSuccess);
        Assert.Equal(
            originalStamp,
            await database.GetSecurityStampAsync(UserId));
    }

    [Fact]
    public async Task Update_WithOwnNormalizedIdentityValues_DoesNotReportDuplicate()
    {
        await using var database = await UserTestDatabase.CreateAsync();
        var service = database.CreateService(AdminId);

        var result = await service.UpdateAsync(
            UserId,
            new UserUpdateRequest(
                "  STANDARD-USER  ",
                "  STANDARD-USER@EXAMPLE.COM  ",
                "Changed",
                "Name",
                null,
                [ApplicationRoles.User],
                [1]));

        Assert.True(result.IsSuccess);
        Assert.Equal("STANDARD-USER", result.Value.UserName);
        Assert.Equal("STANDARD-USER@EXAMPLE.COM", result.Value.Email);
    }

    [Fact]
    public async Task Update_WithAnotherUsersEmail_ReturnsConflict()
    {
        await using var database = await UserTestDatabase.CreateAsync();
        var service = database.CreateService(AdminId);

        var result = await service.UpdateAsync(
            UserId,
            new UserUpdateRequest(
                "standard-user",
                "admin-user@example.com",
                "Changed",
                "Name",
                null,
                [ApplicationRoles.User],
                [1]));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal("Users.EmailExists", result.Error.Code);
        Assert.Equal(nameof(UserUpdateRequest.Email), result.Error.FieldName);
    }

    [Fact]
    public async Task Update_WhenRoleAssignmentFails_RollsBackAllChanges()
    {
        await using var database = await UserTestDatabase.CreateAsync();
        var service = database.CreateService(
            AdminId,
            database.CreateFailingRoleUserManager());

        var result = await service.UpdateAsync(
            UserId,
            new UserUpdateRequest(
                "changed-user",
                "changed-user@example.com",
                "Changed",
                "User",
                null,
                [ApplicationRoles.Admin],
                [2]));

        Assert.True(result.IsFailure);
        database.Context.ChangeTracker.Clear();
        var user = await database.Context.Users
            .AsNoTracking()
            .SingleAsync(entity => entity.Id == UserId);
        var roles = await database.GetRolesAsync(UserId);
        var companyIds = await database.GetCompanyIdsAsync(UserId);
        Assert.Equal("standard-user", user.UserName);
        Assert.Equal(
            [ApplicationRoles.User],
            roles);
        Assert.Equal([1], companyIds);
    }

    [Fact]
    public async Task AssignCompanies_ReplacesTheCompleteSet()
    {
        await using var database = await UserTestDatabase.CreateAsync();
        var service = database.CreateService(AdminId);

        var result = await service.AssignCompaniesAsync(
            UserId,
            new UserCompaniesRequest([2]));

        Assert.True(result.IsSuccess);
        var companyIds = await database.GetCompanyIdsAsync(UserId);
        Assert.Equal([2], result.Value.Companies.Select(company => company.Id));
        Assert.Equal([2], companyIds);
    }

    [Fact]
    public async Task Delete_CurrentUser_ReturnsConflict()
    {
        await using var database = await UserTestDatabase.CreateAsync();
        var service = database.CreateService(AdminId);

        var result = await service.DeleteAsync(AdminId);

        Assert.True(result.IsFailure);
        Assert.Equal("Users.CannotDeleteCurrentUser", result.Error.Code);
    }

    [Fact]
    public async Task Delete_LastAdmin_ReturnsConflict()
    {
        await using var database = await UserTestDatabase.CreateAsync();
        var service = database.CreateService(UserId);

        var result = await service.DeleteAsync(AdminId);

        Assert.True(result.IsFailure);
        Assert.Equal("Users.LastAdmin", result.Error.Code);
    }

    [Fact]
    public async Task Delete_OrdinaryUser_RemovesUserAndAssignments()
    {
        await using var database = await UserTestDatabase.CreateAsync();
        var service = database.CreateService(AdminId);

        var result = await service.DeleteAsync(UserId);

        Assert.True(result.IsSuccess);
        Assert.Null(await database.UserManager.FindByIdAsync(UserId.ToString()));
        Assert.Empty(await database.GetCompanyIdsAsync(UserId));
    }

    [Theory]
    [InlineData(
        nameof(IdentityErrorDescriber.DuplicateUserName),
        "Users.UserNameExists",
        nameof(UserCreateRequest.UserName))]
    [InlineData(
        nameof(IdentityErrorDescriber.DuplicateEmail),
        "Users.EmailExists",
        nameof(UserCreateRequest.Email))]
    public async Task Add_WhenIdentityDetectsDuplicate_ReturnsConflict(
        string identityCode,
        string expectedCode,
        string expectedField)
    {
        await using var database = await UserTestDatabase.CreateAsync();
        var service = database.CreateService(
            AdminId,
            database.CreateDuplicateUserManager(identityCode));

        var result = await service.AddAsync(
            new UserCreateRequest(
                "concurrent-user",
                "concurrent-user@example.com",
                "Concurrent",
                "User",
                null,
                "P@ssword123",
                [ApplicationRoles.User],
                [1]));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal(expectedCode, result.Error.Code);
        Assert.Equal(expectedField, result.Error.FieldName);
    }

    private sealed class UserTestDatabase : IAsyncDisposable
    {
        private UserTestDatabase(
            SqliteConnection connection,
            ServiceProvider serviceProvider,
            AsyncServiceScope scope,
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole<Guid>> roleManager)
        {
            Connection = connection;
            ServiceProvider = serviceProvider;
            Scope = scope;
            Context = context;
            UserManager = userManager;
            RoleManager = roleManager;
        }

        private SqliteConnection Connection { get; }

        private ServiceProvider ServiceProvider { get; }

        private AsyncServiceScope Scope { get; }

        public ApplicationDbContext Context { get; }

        public UserManager<ApplicationUser> UserManager { get; }

        private RoleManager<IdentityRole<Guid>> RoleManager { get; }

        public static async Task<UserTestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var services = new ServiceCollection();
            services.AddInfrastructure(CreateConfiguration());
            services.RemoveAll<ApplicationDbContext>();
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<
                IDbContextOptionsConfiguration<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(
                options => options.UseSqlite(connection));

            var serviceProvider = services.BuildServiceProvider();
            var scope = serviceProvider.CreateAsyncScope();
            var context = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();
            var userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = scope.ServiceProvider
                .GetRequiredService<RoleManager<IdentityRole<Guid>>>();

            await context.Database.EnsureCreatedAsync();
            await SeedAsync(context, userManager, roleManager);

            return new UserTestDatabase(
                connection,
                serviceProvider,
                scope,
                context,
                userManager,
                roleManager);
        }

        public UserService CreateService(
            Guid currentUserId,
            UserManager<ApplicationUser>? userManager = null) =>
            new(
                Context,
                userManager ?? UserManager,
                RoleManager,
                new TestCurrentUserService(currentUserId));

        public UserManager<ApplicationUser> CreateFailingRoleUserManager() =>
            CreateUserManager(failRoleAssignment: true);

        public UserManager<ApplicationUser> CreateDuplicateUserManager(
            string identityCode) =>
            CreateUserManager(
                createFailure: IdentityResult.Failed(
                    new IdentityError
                    {
                        Code = identityCode,
                        Description = "Duplicate identity value."
                    }));

        public async Task<string?> GetSecurityStampAsync(Guid userId)
        {
            Context.ChangeTracker.Clear();
            return await Context.Users
                .AsNoTracking()
                .Where(user => user.Id == userId)
                .Select(user => user.SecurityStamp)
                .SingleAsync();
        }

        public async Task<int[]> GetCompanyIdsAsync(Guid userId)
        {
            Context.ChangeTracker.Clear();
            return await Context.UserCompanies
                .AsNoTracking()
                .Where(userCompany => userCompany.UserId == userId)
                .OrderBy(userCompany => userCompany.CompanyId)
                .Select(userCompany => userCompany.CompanyId)
                .ToArrayAsync();
        }

        public async Task<string[]> GetRolesAsync(Guid userId)
        {
            Context.ChangeTracker.Clear();
            return await Context.UserRoles
                .Where(userRole => userRole.UserId == userId)
                .Join(
                    Context.Roles,
                    userRole => userRole.RoleId,
                    role => role.Id,
                    (_, role) => role.Name!)
                .OrderBy(role => role)
                .ToArrayAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await Scope.DisposeAsync();
            await ServiceProvider.DisposeAsync();
            await Connection.DisposeAsync();
        }

        private TestUserManager CreateUserManager(
            IdentityResult? createFailure = null,
            bool failRoleAssignment = false)
        {
            var services = Scope.ServiceProvider;
            return new TestUserManager(
                services.GetRequiredService<IUserStore<ApplicationUser>>(),
                services.GetRequiredService<IOptions<IdentityOptions>>(),
                services.GetRequiredService<IPasswordHasher<ApplicationUser>>(),
                services.GetServices<IUserValidator<ApplicationUser>>(),
                services.GetServices<IPasswordValidator<ApplicationUser>>(),
                services.GetRequiredService<ILookupNormalizer>(),
                services.GetRequiredService<IdentityErrorDescriber>(),
                services,
                services.GetRequiredService<
                    ILogger<UserManager<ApplicationUser>>>(),
                createFailure,
                failRoleAssignment);
        }

        private static IConfiguration CreateConfiguration() =>
            new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DefaultConnection"] =
                            "Server=(local);Database=MiniErpTests",
                        ["Identity:User:RequireUniqueEmail"] = "true",
                        ["Jwt:Issuer"] = "MiniErp.Tests",
                        ["Jwt:SigningKey"] =
                            "MiniErp-tests-signing-key-at-least-32-bytes",
                        ["Jwt:ClockSkewSeconds"] = "0",
                        ["Jwt:AccessToken:Audience"] =
                            "MiniErp.Tests.Client",
                        ["Jwt:AccessToken:ExpirationMinutes"] = "15",
                        ["Jwt:CompanySelectionToken:Audience"] =
                            "MiniErp.Tests.CompanySelection",
                        ["Jwt:CompanySelectionToken:ExpirationMinutes"] = "5",
                        ["Jwt:RefreshToken:ExpirationDays"] = "1"
                    })
                .Build();

        private static async Task SeedAsync(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole<Guid>> roleManager)
        {
            await CreateRoleAsync(roleManager, ApplicationRoles.Admin);
            await CreateRoleAsync(roleManager, ApplicationRoles.User);

            await InsertCompanyAsync(context, 1, "Company One");
            await InsertCompanyAsync(context, 2, "Company Two");

            await CreateUserAsync(
                context,
                userManager,
                AdminId,
                "admin-user",
                "admin-user@example.com",
                ApplicationRoles.Admin);
            await CreateUserAsync(
                context,
                userManager,
                UserId,
                "standard-user",
                "standard-user@example.com",
                ApplicationRoles.User);
            context.ChangeTracker.Clear();
        }

        private static Task InsertCompanyAsync(
            ApplicationDbContext context,
            int id,
            string name) =>
            context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 INSERT INTO Companies (
                     Id,
                     Name,
                     Address,
                     CommercialRegister,
                     TaxNumber,
                     ManagerName,
                     RowVersion,
                     CreatedById,
                     CreatedOn,
                     CreatedByPc,
                     IsDeleted)
                 VALUES (
                     {id},
                     {name},
                     {$"{name} Address"},
                     {$"CR-{id}"},
                     {$"TAX-{id}"},
                     {$"{name} Manager"},
                     randomblob(8),
                     {"System"},
                     {DateTime.UtcNow},
                     {"Tests"},
                     {false})
                 """);

        private static async Task CreateRoleAsync(
            RoleManager<IdentityRole<Guid>> roleManager,
            string roleName)
        {
            var result = await roleManager.CreateAsync(
                new IdentityRole<Guid>
                {
                    Id = Guid.NewGuid(),
                    Name = roleName
                });
            Assert.True(result.Succeeded);
        }

        private static async Task CreateUserAsync(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            Guid id,
            string userName,
            string email,
            string role)
        {
            var user = new ApplicationUser
            {
                Id = id,
                UserName = userName,
                Email = email,
                EmailConfirmed = true,
                FirstName = userName,
                LastName = "Test",
                ProfileImage = string.Empty
            };

            Assert.True((await userManager.CreateAsync(
                user,
                "P@ssword123")).Succeeded);
            Assert.True((await userManager.AddToRoleAsync(user, role)).Succeeded);

            context.UserCompanies.Add(new UserCompany
            {
                UserId = id,
                CompanyId = 1
            });
            await context.SaveChangesAsync();
        }

    }

    private sealed class TestUserManager(
        IUserStore<ApplicationUser> store,
        IOptions<IdentityOptions> optionsAccessor,
        IPasswordHasher<ApplicationUser> passwordHasher,
        IEnumerable<IUserValidator<ApplicationUser>> userValidators,
        IEnumerable<IPasswordValidator<ApplicationUser>> passwordValidators,
        ILookupNormalizer keyNormalizer,
        IdentityErrorDescriber errors,
        IServiceProvider services,
        ILogger<UserManager<ApplicationUser>> logger,
        IdentityResult? createFailure,
        bool failRoleAssignment)
        : UserManager<ApplicationUser>(
            store,
            optionsAccessor,
            passwordHasher,
            userValidators,
            passwordValidators,
            keyNormalizer,
            errors,
            services,
            logger)
    {
        public override Task<ApplicationUser?> FindByNameAsync(string userName) =>
            createFailure is null
                ? base.FindByNameAsync(userName)
                : Task.FromResult<ApplicationUser?>(null);

        public override Task<ApplicationUser?> FindByEmailAsync(string email) =>
            createFailure is null
                ? base.FindByEmailAsync(email)
                : Task.FromResult<ApplicationUser?>(null);

        public override Task<IdentityResult> CreateAsync(
            ApplicationUser user,
            string password) =>
            createFailure is null
                ? base.CreateAsync(user, password)
                : Task.FromResult(createFailure);

        public override Task<IdentityResult> AddToRolesAsync(
            ApplicationUser user,
            IEnumerable<string> roles) =>
            failRoleAssignment
                ? Task.FromResult(
                    IdentityResult.Failed(
                        new IdentityError
                        {
                            Code = "Test.RoleAssignmentFailed",
                            Description = "Role assignment failed."
                        }))
                : base.AddToRolesAsync(user, roles);
    }

    private sealed class TestCurrentUserService(Guid userId)
        : ICurrentUserService
    {
        public Result<Guid> GetUserId() => Result<Guid>.Success(userId);
    }
}
