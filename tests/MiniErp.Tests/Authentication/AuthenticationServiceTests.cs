using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using MiniErp.Application.Features.Authentication;
using MiniErp.Infrastructure;
using MiniErp.Infrastructure.Identity;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Tests.Authentication;

public sealed class AuthenticationServiceTests
{
    [Fact]
    public async Task Refresh_WithCurrentSecurityStamp_Succeeds()
    {
        await using var database =
            await AuthenticationServiceTestDatabase.CreateAsync();
        var loginResult = await database.Service.LoginAsync(
            new LoginRequest(
                UserName: AuthenticationServiceTestDatabase.UserName,
                Password: AuthenticationServiceTestDatabase.Password));

        Assert.True(loginResult.IsSuccess);
        Assert.NotNull(loginResult.Value.RefreshToken);

        database.Context.ChangeTracker.Clear();
        var storedToken = await database.Context.RefreshTokens
            .AsNoTracking()
            .SingleAsync();
        var currentSecurityStamp = await database.Context.Users
            .AsNoTracking()
            .Where(user =>
                user.Id == AuthenticationServiceTestDatabase.UserId)
            .Select(user => user.SecurityStamp)
            .SingleAsync();

        Assert.False(string.IsNullOrEmpty(currentSecurityStamp));
        Assert.Equal(currentSecurityStamp, storedToken.SecurityStampSnapshot);

        var refreshResult = await database.Service.RefreshAsync(
            new RefreshTokenRequest(
                RefreshToken: loginResult.Value.RefreshToken!));

        Assert.True(refreshResult.IsSuccess);
        Assert.NotEmpty(refreshResult.Value.AccessToken);
        Assert.NotEmpty(refreshResult.Value.RefreshToken);
    }

    [Fact]
    public async Task Refresh_AfterSecurityStampChanges_ReturnsInvalidToken()
    {
        await using var database =
            await AuthenticationServiceTestDatabase.CreateAsync();
        var loginResult = await database.Service.LoginAsync(
            new LoginRequest(
                UserName: AuthenticationServiceTestDatabase.UserName,
                Password: AuthenticationServiceTestDatabase.Password));
        Assert.True(loginResult.IsSuccess);

        database.Context.ChangeTracker.Clear();
        var user = await database.UserManager.FindByIdAsync(
            AuthenticationServiceTestDatabase.UserId.ToString());
        Assert.NotNull(user);
        var originalSecurityStamp = user.SecurityStamp;
        var stampResult = await database.UserManager
            .UpdateSecurityStampAsync(user);

        Assert.True(stampResult.Succeeded);
        Assert.NotEqual(originalSecurityStamp, user.SecurityStamp);

        var refreshResult = await database.Service.RefreshAsync(
            new RefreshTokenRequest(
                RefreshToken: loginResult.Value.RefreshToken!));

        Assert.True(refreshResult.IsFailure);
        Assert.Equal(
            "Authentication.InvalidRefreshToken",
            refreshResult.Error.Code);
    }

    [Fact]
    public async Task Refresh_AfterPasswordChanges_ReturnsInvalidToken()
    {
        await using var database =
            await AuthenticationServiceTestDatabase.CreateAsync();
        var loginResult = await database.Service.LoginAsync(
            new LoginRequest(
                UserName: AuthenticationServiceTestDatabase.UserName,
                Password: AuthenticationServiceTestDatabase.Password));
        Assert.True(loginResult.IsSuccess);

        database.Context.ChangeTracker.Clear();
        var user = await database.UserManager.FindByIdAsync(
            AuthenticationServiceTestDatabase.UserId.ToString());
        Assert.NotNull(user);
        var originalSecurityStamp = user.SecurityStamp;
        var passwordResult = await database.UserManager.ChangePasswordAsync(
            user,
            AuthenticationServiceTestDatabase.Password,
            "Changed!12345");

        Assert.True(passwordResult.Succeeded);
        Assert.NotEqual(originalSecurityStamp, user.SecurityStamp);

        var refreshResult = await database.Service.RefreshAsync(
            new RefreshTokenRequest(
                RefreshToken: loginResult.Value.RefreshToken!));

        Assert.True(refreshResult.IsFailure);
        Assert.Equal(
            "Authentication.InvalidRefreshToken",
            refreshResult.Error.Code);
    }

    [Fact]
    public async Task Refresh_WhileUserIsLockedOut_ReturnsInvalidToken()
    {
        await using var database =
            await AuthenticationServiceTestDatabase.CreateAsync();
        var loginResult = await database.Service.LoginAsync(
            new LoginRequest(
                UserName: AuthenticationServiceTestDatabase.UserName,
                Password: AuthenticationServiceTestDatabase.Password));
        Assert.True(loginResult.IsSuccess);

        database.Context.ChangeTracker.Clear();
        var user = await database.UserManager.FindByIdAsync(
            AuthenticationServiceTestDatabase.UserId.ToString());
        Assert.NotNull(user);
        var lockoutResult = await database.UserManager.SetLockoutEndDateAsync(
            user,
            TimeProvider.System.GetUtcNow().AddHours(1));

        Assert.True(lockoutResult.Succeeded);
        Assert.True(await database.UserManager.IsLockedOutAsync(user));

        var refreshResult = await database.Service.RefreshAsync(
            new RefreshTokenRequest(
                RefreshToken: loginResult.Value.RefreshToken!));

        Assert.True(refreshResult.IsFailure);
        Assert.Equal(
            "Authentication.InvalidRefreshToken",
            refreshResult.Error.Code);
    }

    [Fact]
    public async Task Login_WithoutSecurityStamp_ReturnsInvalidUserContext()
    {
        await using var database =
            await AuthenticationServiceTestDatabase.CreateAsync();
        await database.Context.Users
            .Where(user =>
                user.Id == AuthenticationServiceTestDatabase.UserId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(
                user => user.SecurityStamp,
                (string?)null));
        database.Context.ChangeTracker.Clear();

        var loginResult = await database.Service.LoginAsync(
            new LoginRequest(
                UserName: AuthenticationServiceTestDatabase.UserName,
                Password: AuthenticationServiceTestDatabase.Password));

        Assert.True(loginResult.IsFailure);
        Assert.Equal(
            "Authentication.InvalidUserContext",
            loginResult.Error.Code);
        Assert.Empty(await database.Context.RefreshTokens.ToListAsync());
    }

    private sealed class AuthenticationServiceTestDatabase : IAsyncDisposable
    {
        public static readonly Guid UserId =
            Guid.Parse("33333333-3333-3333-3333-333333333333");

        public const string UserName = "refresh-test-user";
        public const string Password = "Test!12345";

        private AuthenticationServiceTestDatabase(
            SqliteConnection connection,
            ServiceProvider serviceProvider,
            AsyncServiceScope scope,
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            AuthenticationService service)
        {
            Connection = connection;
            ServiceProvider = serviceProvider;
            Scope = scope;
            Context = context;
            UserManager = userManager;
            Service = service;
        }

        private SqliteConnection Connection { get; }

        private ServiceProvider ServiceProvider { get; }

        private AsyncServiceScope Scope { get; }

        public ApplicationDbContext Context { get; }

        public UserManager<ApplicationUser> UserManager { get; }

        public AuthenticationService Service { get; }

        public static async Task<AuthenticationServiceTestDatabase>
            CreateAsync()
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
            var signInManager = scope.ServiceProvider
                .GetRequiredService<SignInManager<ApplicationUser>>();
            var jwtOptions = scope.ServiceProvider
                .GetRequiredService<IOptions<JwtOptions>>();

            await context.Database.EnsureCreatedAsync();
            await SeedAsync(context, userManager);

            var service = new AuthenticationService(
                userManager,
                signInManager,
                context,
                jwtOptions,
                TimeProvider.System);

            return new AuthenticationServiceTestDatabase(
                connection,
                serviceProvider,
                scope,
                context,
                userManager,
                service);
        }

        public async ValueTask DisposeAsync()
        {
            await Scope.DisposeAsync();
            await ServiceProvider.DisposeAsync();
            await Connection.DisposeAsync();
        }

        private static IConfiguration CreateConfiguration() =>
            new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DefaultConnection"] =
                            "Server=(local);Database=MiniErpTests",
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
            UserManager<ApplicationUser> userManager)
        {
            var user = new ApplicationUser
            {
                Id = UserId,
                UserName = UserName,
                Email = "refresh-test-user@example.com",
                FirstName = "Refresh",
                LastName = "Tester",
                ProfileImage = string.Empty,
                LockoutEnabled = true
            };
            var createResult = await userManager.CreateAsync(user, Password);
            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(
                    string.Join(
                        "; ",
                        createResult.Errors.Select(error =>
                            error.Description)));
            }

            await context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO Companies (
                    Id, Name, Address, CommercialRegister, TaxNumber,
                    ManagerName, RowVersion, CreatedById, CreatedOn,
                    CreatedByPc, IsDeleted)
                VALUES (
                    1, 'Company One', 'Address', 'CR-1', 'TAX-1',
                    'Manager', X'01', 'test', '2026-01-01',
                    'test', 0);
                """);
            context.UserCompanies.Add(
                new UserCompany
                {
                    UserId = UserId,
                    CompanyId = 1
                });
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();
        }
    }
}
