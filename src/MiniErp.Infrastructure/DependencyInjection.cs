using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using MiniErp.Application.Common.Authentication;
using MiniErp.Infrastructure.Identity;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Application.Features.ExchangeRates;
using MiniErp.Infrastructure.Services.ExchangeRates;
using MiniErp.Infrastructure.Persistence.Interceptors;

namespace MiniErp.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' was not found.");

        services.AddHttpContextAccessor();
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<AuditableEntityInterceptor>();
        services.AddOptions<FrankfurterOptions>()
            .Bind(configuration.GetSection(FrankfurterOptions.SectionName))
            .Validate(options =>
                Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri) &&
                uri.Scheme is "http" or "https",
                "Frankfurter:BaseUrl must be an absolute HTTP or HTTPS URI.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Provider),
                "Frankfurter:Provider is required.")
            .Validate(options => options.TimeoutSeconds is > 0 and <= 120,
                "Frankfurter:TimeoutSeconds must be between 1 and 120.")
            .ValidateOnStart();
        services.AddHttpClient<IExchangeRateProvider, FrankfurterExchangeRateProvider>(
            (serviceProvider, client) =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<FrankfurterOptions>>().Value;
                client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute);
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));
            });

        services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
            options
                .UseSqlServer(connectionString)
                .AddInterceptors(
                    serviceProvider.GetRequiredService<AuditableEntityInterceptor>()));

        services
            .AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole<Guid>>()
            .AddErrorDescriber<ArabicIdentityErrorDescriber>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services.Configure<IdentityOptions>(
            configuration.GetSection("Identity"));

        var jwtSection = configuration.GetSection(JwtOptions.SectionName);
        var jwtOptions = jwtSection.Get<JwtOptions>()
            ?? throw new InvalidOperationException(
                $"Configuration section '{JwtOptions.SectionName}' was not found.");

        ValidateJwtOptions(jwtOptions);
        services.Configure<JwtOptions>(jwtSection);

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.AccessToken.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(
                        jwtOptions.ClockSkewSeconds),
                    NameClaimType = "unique_name",
                    RoleClaimType = "role"
                };
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(accessToken) &&
                            context.HttpContext.Request.Path.StartsWithSegments(
                                "/hubs/updates"))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    },
                    OnTokenValidated = async context =>
                    {
                        var tokenUse = context.Principal?
                            .FindFirst(CustomClaimTypes.TokenUse)?
                            .Value;
                        if (!string.Equals(
                                tokenUse,
                                CustomClaimTypes.AccessTokenUse,
                                StringComparison.Ordinal))
                        {
                            context.Fail("الرمز المستخدم ليس رمز وصول.");
                            return;
                        }

                        if (!CompanyClaimResolver.TryGetCompanyId(
                                context.Principal,
                                out var companyId))
                        {
                            context.Fail(
                                "يجب أن يحتوي رمز الوصول على قيمة company_id واحدة وصحيحة.");
                            return;
                        }

                        var userIdValue = context.Principal?
                            .FindFirst("sub")?
                            .Value;
                        if (!Guid.TryParse(userIdValue, out var userId) ||
                            userId == Guid.Empty)
                        {
                            context.Fail(
                                "يجب أن يحتوي رمز الوصول على رقم مستخدم صحيح.");
                            return;
                        }

                        var tokenSecurityStamp = context.Principal?
                            .FindFirst(CustomClaimTypes.SecurityStamp)?
                            .Value;
                        if (string.IsNullOrEmpty(tokenSecurityStamp))
                        {
                            context.Fail(
                                "رمز الوصول لا يحتوي على بيانات الجلسة المطلوبة.");
                            return;
                        }

                        var dbContext = context.HttpContext.RequestServices
                            .GetRequiredService<ApplicationDbContext>();
                        var currentSecurityStamp = await dbContext.UserCompanies
                            .AsNoTracking()
                            .Where(userCompany =>
                                    userCompany.UserId == userId &&
                                    userCompany.CompanyId == companyId)
                            .Select(userCompany =>
                                userCompany.User.SecurityStamp)
                            .SingleOrDefaultAsync(
                                context.HttpContext.RequestAborted);

                        if (!string.Equals(
                                currentSecurityStamp,
                                tokenSecurityStamp,
                                StringComparison.Ordinal))
                        {
                            context.Fail(
                                "انتهت صلاحية الجلسة أو لم يعد المستخدم يملك صلاحية الوصول إلى الشركة.");
                        }
                    }
                };
            });

        return services;
    }

    private static void ValidateJwtOptions(JwtOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Issuer) ||
            string.IsNullOrWhiteSpace(options.AccessToken.Audience) ||
            string.IsNullOrWhiteSpace(
                options.CompanySelectionToken.Audience))
        {
            throw new InvalidOperationException(
                "Jwt:Issuer, Jwt:AccessToken:Audience, and " +
                "Jwt:CompanySelectionToken:Audience must be configured.");
        }

        if (Encoding.UTF8.GetByteCount(options.SigningKey) < 32)
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey must contain at least 32 bytes.");
        }

        if (options.AccessToken.ExpirationMinutes <= 0 ||
            options.CompanySelectionToken.ExpirationMinutes <= 0 ||
            options.RefreshToken.ExpirationDays <= 0)
        {
            throw new InvalidOperationException(
                "JWT token expiration values must be greater than zero.");
        }

        if (options.ClockSkewSeconds < 0)
        {
            throw new InvalidOperationException(
                "Jwt:ClockSkewSeconds cannot be negative.");
        }
    }
}
