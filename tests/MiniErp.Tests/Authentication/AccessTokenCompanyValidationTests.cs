using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using MiniErp.Application.Common.Authentication;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Infrastructure;
using MiniErp.Infrastructure.Identity;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Tests.Authentication;

public sealed class AccessTokenCompanyValidationTests
{
    [Fact]
    public async Task TokenValidation_WithCurrentCompanyAssignment_Succeeds()
    {
        await using var database =
            await AccessTokenCompanyTestDatabase.CreateAsync();

        var context = await database.ValidateAccessTokenAsync();

        Assert.Null(context.Result);
    }

    [Fact]
    public async Task TokenValidation_AfterCompanyAssignmentIsRemoved_Fails()
    {
        await using var database =
            await AccessTokenCompanyTestDatabase.CreateAsync();
        await database.RemoveAssignmentAsync();

        var context = await database.ValidateAccessTokenAsync();

        Assert.NotNull(context.Result?.Failure);
    }

    [Fact]
    public async Task TokenValidation_AfterCompanyIsSoftDeleted_Fails()
    {
        await using var database =
            await AccessTokenCompanyTestDatabase.CreateAsync();
        await database.SoftDeleteCompanyAsync();

        var context = await database.ValidateAccessTokenAsync();

        Assert.NotNull(context.Result?.Failure);
    }

    [Fact]
    public async Task TokenValidation_WithStaleSecurityStamp_Fails()
    {
        await using var database =
            await AccessTokenCompanyTestDatabase.CreateAsync();

        var context = await database.ValidateAccessTokenAsync(
            securityStamp: "stale-security-stamp");

        Assert.NotNull(context.Result?.Failure);
    }

    [Fact]
    public async Task TokenValidation_WithoutSecurityStamp_Fails()
    {
        await using var database =
            await AccessTokenCompanyTestDatabase.CreateAsync();

        var context = await database.ValidateAccessTokenAsync(
            securityStamp: null);

        Assert.NotNull(context.Result?.Failure);
    }

    private sealed class AccessTokenCompanyTestDatabase : IAsyncDisposable
    {
        private static readonly Guid UserId =
            Guid.Parse("22222222-2222-2222-2222-222222222222");

        private const int CompanyId = 1;
        private const string SecurityStamp = "current-security-stamp";

        private AccessTokenCompanyTestDatabase(
            SqliteConnection connection,
            ServiceProvider serviceProvider,
            AsyncServiceScope scope,
            ApplicationDbContext context)
        {
            Connection = connection;
            ServiceProvider = serviceProvider;
            Scope = scope;
            Context = context;
        }

        private SqliteConnection Connection { get; }

        private ServiceProvider ServiceProvider { get; }

        private AsyncServiceScope Scope { get; }

        private ApplicationDbContext Context { get; }

        public static async Task<AccessTokenCompanyTestDatabase> CreateAsync()
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
            await context.Database.EnsureCreatedAsync();
            await SeedAsync(context);

            return new AccessTokenCompanyTestDatabase(
                connection,
                serviceProvider,
                scope,
                context);
        }

        public async Task RemoveAssignmentAsync()
        {
            var assignment = await Context.UserCompanies
                .SingleAsync(userCompany =>
                    userCompany.UserId == UserId &&
                    userCompany.CompanyId == CompanyId);
            Context.UserCompanies.Remove(assignment);
            await Context.SaveChangesAsync();
            Context.ChangeTracker.Clear();
        }

        public async Task SoftDeleteCompanyAsync()
        {
            var company = await Context.Companies.SingleAsync(
                entity => entity.Id == CompanyId);
            company.IsDeleted = true;
            await Context.SaveChangesAsync();
            Context.ChangeTracker.Clear();
        }

        public async Task<TokenValidatedContext> ValidateAccessTokenAsync(
            string? securityStamp = SecurityStamp)
        {
            var options = Scope.ServiceProvider
                .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
                .Get(JwtBearerDefaults.AuthenticationScheme);
            var httpContext = new DefaultHttpContext
            {
                RequestServices = Scope.ServiceProvider
            };
            var scheme = new AuthenticationScheme(
                JwtBearerDefaults.AuthenticationScheme,
                displayName: null,
                typeof(JwtBearerHandler));
            var context = new TokenValidatedContext(
                httpContext,
                scheme,
                options)
            {
                Principal = CreatePrincipal(securityStamp)
            };

            await options.Events.OnTokenValidated(context);

            return context;
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
                        ["Jwt:AccessToken:Audience"] = "MiniErp.Tests.Client",
                        ["Jwt:AccessToken:ExpirationMinutes"] = "15",
                        ["Jwt:CompanySelectionToken:Audience"] =
                            "MiniErp.Tests.CompanySelection",
                        ["Jwt:CompanySelectionToken:ExpirationMinutes"] = "5",
                        ["Jwt:RefreshToken:ExpirationDays"] = "1"
                    })
                .Build();

        private static ClaimsPrincipal CreatePrincipal(string? securityStamp)
        {
            var claims = new List<Claim>
            {
                new("sub", UserId.ToString()),
                new(
                    CustomClaimTypes.TokenUse,
                    CustomClaimTypes.AccessTokenUse),
                new(
                    CustomClaimTypes.CompanyId,
                    CompanyId.ToString())
            };
            if (securityStamp is not null)
            {
                claims.Add(new Claim(
                    CustomClaimTypes.SecurityStamp,
                    securityStamp));
            }

            return new ClaimsPrincipal(
                new ClaimsIdentity(
                    claims,
                    JwtBearerDefaults.AuthenticationScheme));
        }

        private static async Task SeedAsync(ApplicationDbContext context)
        {
            context.Users.Add(
                new ApplicationUser
                {
                    Id = UserId,
                    UserName = "company-test-user",
                    NormalizedUserName = "COMPANY-TEST-USER",
                    FirstName = "Company",
                    LastName = "Tester",
                    ProfileImage = string.Empty,
                    SecurityStamp = SecurityStamp
                });
            context.Companies.Add(
                new Company
                {
                    Id = CompanyId,
                    Name = "Company One",
                    Address = "Address",
                    CommercialRegister = "CR-1",
                    TaxNumber = "TAX-1",
                    ManagerName = "Manager"
                });
            context.UserCompanies.Add(
                new UserCompany
                {
                    UserId = UserId,
                    CompanyId = CompanyId
                });
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();
        }
    }
}
