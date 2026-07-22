using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using MiniErp.Application.Common.Authentication;
using MiniErp.Infrastructure.Identity;
using MiniErp.Infrastructure.Persistence;
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
                    OnTokenValidated = context =>
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
                            return Task.CompletedTask;
                        }

                        if (!CompanyClaimResolver.TryGetCompanyId(
                                context.Principal,
                                out _))
                        {
                            context.Fail(
                                "يجب أن يحتوي رمز الوصول على قيمة company_id واحدة وصحيحة.");
                        }

                        return Task.CompletedTask;
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
