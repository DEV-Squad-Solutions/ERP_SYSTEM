using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MiniErp.Application.Common.Abstractions;
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

        var tokenResult = await CreateTokenPairAsync(user, cancellationToken);

        return Result<LoginResponse>.Success(new LoginResponse(
            tokenResult.Value.AccessToken,
            tokenResult.Value.RefreshToken,
            $"{user.FirstName} {user.LastName}".Trim(),
            user.Email ?? string.Empty));
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

        storedToken.RevokedAtUtc = now;

        try
        {
            return await CreateTokenPairAsync(
                storedToken.User,
                cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return InvalidRefreshToken();
        }
    }

    private async Task<Result<TokenResponse>> CreateTokenPairAsync(
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        var accessToken = await CreateAccessTokenAsync(user);
        var rawRefreshToken = CreateRefreshToken();
        var now = timeProvider.GetUtcNow();

        dbContext.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = HashRefreshToken(rawRefreshToken),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(options.RefreshTokenExpirationDays)
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<TokenResponse>.Success(
            new TokenResponse(accessToken, rawRefreshToken));
    }

    private async Task<string> CreateAccessTokenAsync(ApplicationUser user)
    {
        var now = timeProvider.GetUtcNow();
        var roles = await userManager.GetRolesAsync(user);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(
                JwtRegisteredClaimNames.Iat,
                EpochTime.GetIntDate(now.UtcDateTime).ToString(),
                ClaimValueTypes.Integer64)
        };

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Email, user.Email));
        }

        if (!string.IsNullOrWhiteSpace(user.UserName))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName));
        }

        claims.AddRange(roles.Select(role => new Claim("role", role)));

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(options.SigningKey));
        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: now.AddMinutes(options.AccessTokenExpirationMinutes).UtcDateTime,
            signingCredentials: new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

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
}
