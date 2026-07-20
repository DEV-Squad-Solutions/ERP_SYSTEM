using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Authentication;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.Authentication;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Identity;

public sealed class AuthenticationService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ApplicationDbContext dbContext,
    IOptions<JwtOptions> jwtOptions,
    TimeProvider timeProvider)
    : IAuthenticationService, IScopedService
{
    private readonly JwtOptions options = jwtOptions.Value;

    public async Task<Result<LoginResponse>> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await userManager.FindByNameAsync(request.UserName.Trim());
        if (user is null)
        {
            return InvalidCredentials();
        }

        var signInResult = await signInManager.CheckPasswordSignInAsync(
            user,
            request.Password,
            lockoutOnFailure: true);
        if (!signInResult.Succeeded)
        {
            return InvalidCredentials();
        }

        var companies = await GetAllowedCompaniesAsync(user.Id, cancellationToken);
        if (companies.Count == 0)
        {
            return Result<LoginResponse>.Failure(NoCompanyAccess());
        }

        var roles = (await userManager.GetRolesAsync(user))
            .OrderBy(role => role)
            .ToArray();

        if (companies.Count > 1)
        {
            return Result<LoginResponse>.Success(new LoginResponse(
                RequiresCompanySelection: true,
                SelectionToken: CreateCompanySelectionToken(user),
                AccessToken: null,
                RefreshToken: null,
                FullName: $"{user.FirstName} {user.LastName}".Trim(),
                Email: user.Email ?? string.Empty,
                Roles: roles,
                Companies: companies));
        }

        var tokenResult = await CreateTokenPairAsync(
            user,
            companies[0].Id,
            cancellationToken);

        return Result<LoginResponse>.Success(new LoginResponse(
            RequiresCompanySelection: false,
            SelectionToken: null,
            AccessToken: tokenResult.Value.AccessToken,
            RefreshToken: tokenResult.Value.RefreshToken,
            FullName: $"{user.FirstName} {user.LastName}".Trim(),
            Email: user.Email ?? string.Empty,
            Roles: roles,
            Companies: companies));
    }

    public async Task<Result<TokenResponse>> SelectCompanyAsync(
        SelectCompanyRequest request,
        CancellationToken cancellationToken = default)
    {
        var selectionResult = ValidateCompanySelectionToken(
            request.SelectionToken);
        if (selectionResult.IsFailure)
        {
            return Result<TokenResponse>.Failure(selectionResult.Error);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(
            selectionResult.Value.UserId.ToString());
        if (user is null ||
            !string.Equals(
                user.SecurityStamp,
                selectionResult.Value.SecurityStamp,
                StringComparison.Ordinal) ||
            !await signInManager.CanSignInAsync(user) ||
            await userManager.IsLockedOutAsync(user))
        {
            return InvalidCompanySelectionToken();
        }

        var hasCompanyAccess = await dbContext.UserCompanies
            .AsNoTracking()
            .AnyAsync(
                userCompany =>
                    userCompany.UserId == user.Id &&
                    userCompany.CompanyId == request.CompanyId,
                cancellationToken);
        if (!hasCompanyAccess)
        {
            return Result<TokenResponse>.Failure(
                Error.Forbidden(
                    "Authentication.CompanyAccessDenied",
                    "The user does not have access to the selected company."));
        }

        return await CreateTokenPairAsync(
            user,
            request.CompanyId,
            cancellationToken);
    }

    public async Task<Result<TokenResponse>> RefreshAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = HashRefreshToken(request.RefreshToken);
        var storedToken = await dbContext.RefreshTokens
            .Include(token => token.User)
            .SingleOrDefaultAsync(
                token => token.TokenHash == tokenHash,
                cancellationToken);

        var now = timeProvider.GetUtcNow();
        if (storedToken is null ||
            storedToken.CompanyId is not int companyId ||
            storedToken.RevokedAtUtc is not null ||
            storedToken.ExpiresAtUtc <= now)
        {
            return InvalidRefreshToken();
        }

        if (!await signInManager.CanSignInAsync(storedToken.User) ||
            await userManager.IsLockedOutAsync(storedToken.User))
        {
            return InvalidRefreshToken();
        }

        var stillHasAccess = await dbContext.UserCompanies
            .AsNoTracking()
            .AnyAsync(
                userCompany =>
                    userCompany.UserId == storedToken.UserId &&
                    userCompany.CompanyId == companyId,
                cancellationToken);
        if (!stillHasAccess)
        {
            return InvalidRefreshToken();
        }

        storedToken.RevokedAtUtc = now;

        try
        {
            return await CreateTokenPairAsync(
                storedToken.User,
                companyId,
                cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return InvalidRefreshToken();
        }
    }

    public async Task<Result> LogoutAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = HashRefreshToken(request.RefreshToken);
        var storedToken = await dbContext.RefreshTokens.SingleOrDefaultAsync(
            token => token.TokenHash == tokenHash,
            cancellationToken);

        if (storedToken is null || storedToken.RevokedAtUtc is not null)
        {
            return Result.Success();
        }

        storedToken.RevokedAtUtc = timeProvider.GetUtcNow();

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Success();
        }

        return Result.Success();
    }

    private async Task<Result<TokenResponse>> CreateTokenPairAsync(
        ApplicationUser user,
        int companyId,
        CancellationToken cancellationToken)
    {
        var accessToken = await CreateAccessTokenAsync(user, companyId);
        var rawRefreshToken = CreateRefreshToken();
        var now = timeProvider.GetUtcNow();

        dbContext.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            CompanyId = companyId,
            TokenHash = HashRefreshToken(rawRefreshToken),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(options.RefreshTokenExpirationDays)
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<TokenResponse>.Success(
            new TokenResponse(accessToken, rawRefreshToken));
    }

    private async Task<string> CreateAccessTokenAsync(
        ApplicationUser user,
        int companyId)
    {
        var now = timeProvider.GetUtcNow();
        var roles = await userManager.GetRolesAsync(user);
        var claims = CreateStandardClaims(user, now);

        claims.Add(new Claim(
            CustomClaimTypes.TokenUse,
            CustomClaimTypes.AccessTokenUse));
        claims.Add(new Claim(
            CustomClaimTypes.CompanyId,
            companyId.ToString(CultureInfo.InvariantCulture)));
        claims.AddRange(roles.Select(role => new Claim("role", role)));

        return WriteToken(
            claims,
            options.Audience,
            now,
            now.AddMinutes(options.AccessTokenExpirationMinutes));
    }

    private string CreateCompanySelectionToken(ApplicationUser user)
    {
        var now = timeProvider.GetUtcNow();
        var claims = CreateStandardClaims(user, now);
        claims.Add(new Claim(
            CustomClaimTypes.TokenUse,
            CustomClaimTypes.CompanySelectionTokenUse));
        claims.Add(new Claim(
            CustomClaimTypes.SecurityStamp,
            user.SecurityStamp ?? string.Empty));

        return WriteToken(
            claims,
            CompanySelectionAudience,
            now,
            now.AddMinutes(options.CompanySelectionTokenExpirationMinutes));
    }

    private Result<CompanySelectionTokenData> ValidateCompanySelectionToken(
        string selectionToken)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler
            {
                MapInboundClaims = false
            };
            var principal = tokenHandler.ValidateToken(
                selectionToken,
                CreateTokenValidationParameters(CompanySelectionAudience),
                out var validatedToken);

            if (validatedToken is not JwtSecurityToken jwtToken ||
                !string.Equals(
                    jwtToken.Header.Alg,
                    SecurityAlgorithms.HmacSha256,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    principal.FindFirst(CustomClaimTypes.TokenUse)?.Value,
                    CustomClaimTypes.CompanySelectionTokenUse,
                    StringComparison.Ordinal) ||
                !Guid.TryParse(
                    principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value,
                    out var userId) ||
                userId == Guid.Empty)
            {
                return InvalidCompanySelectionTokenData();
            }

            var securityStamp = principal
                .FindFirst(CustomClaimTypes.SecurityStamp)?
                .Value;
            return string.IsNullOrEmpty(securityStamp)
                ? InvalidCompanySelectionTokenData()
                : Result<CompanySelectionTokenData>.Success(
                    new CompanySelectionTokenData(userId, securityStamp));
        }
        catch (Exception exception) when (
            exception is SecurityTokenException or ArgumentException)
        {
            return InvalidCompanySelectionTokenData();
        }
    }

    private List<Claim> CreateStandardClaims(
        ApplicationUser user,
        DateTimeOffset now)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(
                JwtRegisteredClaimNames.Iat,
                EpochTime.GetIntDate(now.UtcDateTime).ToString(
                    CultureInfo.InvariantCulture),
                ClaimValueTypes.Integer64)
        };

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Email, user.Email));
        }

        if (!string.IsNullOrWhiteSpace(user.UserName))
        {
            claims.Add(new Claim(
                JwtRegisteredClaimNames.UniqueName,
                user.UserName));
        }

        return claims;
    }

    private string WriteToken(
        IEnumerable<Claim> claims,
        string audience,
        DateTimeOffset notBefore,
        DateTimeOffset expires)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(options.SigningKey));
        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: audience,
            claims: claims,
            notBefore: notBefore.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private TokenValidationParameters CreateTokenValidationParameters(
        string audience) =>
        new()
        {
            ValidateIssuer = true,
            ValidIssuer = options.Issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(options.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = JwtRegisteredClaimNames.UniqueName,
            RoleClaimType = "role"
        };

    private Task<List<CompanyAccessResponse>> GetAllowedCompaniesAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        dbContext.UserCompanies
            .AsNoTracking()
            .Where(userCompany => userCompany.UserId == userId)
            .OrderBy(userCompany => userCompany.Company.Name)
            .ThenBy(userCompany => userCompany.CompanyId)
            .Select(userCompany => new CompanyAccessResponse(
                userCompany.CompanyId,
                userCompany.Company.Name))
            .ToListAsync(cancellationToken);

    private string CompanySelectionAudience =>
        $"{options.Audience}.CompanySelection";

    private static string CreateRefreshToken() =>
        Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));

    private static string HashRefreshToken(string refreshToken) =>
        Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));

    private static Result<LoginResponse> InvalidCredentials() =>
        Result<LoginResponse>.Failure(
            Error.Unauthorized(
                "Authentication.InvalidCredentials",
                "The username or password is incorrect."));

    private static Result<TokenResponse> InvalidRefreshToken() =>
        Result<TokenResponse>.Failure(
            Error.Unauthorized(
                "Authentication.InvalidRefreshToken",
                "The refresh token is invalid or expired."));

    private static Result<TokenResponse> InvalidCompanySelectionToken() =>
        Result<TokenResponse>.Failure(InvalidCompanySelectionTokenError());

    private static Result<CompanySelectionTokenData>
        InvalidCompanySelectionTokenData() =>
        Result<CompanySelectionTokenData>.Failure(
            InvalidCompanySelectionTokenError());

    private static Error InvalidCompanySelectionTokenError() =>
        Error.Unauthorized(
            "Authentication.InvalidCompanySelectionToken",
            "The company-selection token is invalid or expired.");

    private static Error NoCompanyAccess() =>
        Error.Forbidden(
            "Authentication.NoCompanyAccess",
            "The user is not assigned to an active company.");

    private sealed record CompanySelectionTokenData(
        Guid UserId,
        string SecurityStamp);
}
