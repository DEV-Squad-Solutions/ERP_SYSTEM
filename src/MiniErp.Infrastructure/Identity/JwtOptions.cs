namespace MiniErp.Infrastructure.Identity;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = string.Empty;

    public string SigningKey { get; init; } = string.Empty;

    public int ClockSkewSeconds { get; init; } = 30;

    public JwtTokenOptions AccessToken { get; init; } = new();

    public JwtTokenOptions CompanySelectionToken { get; init; } = new();

    public RefreshTokenOptions RefreshToken { get; init; } = new();
}

public sealed class JwtTokenOptions
{
    public string Audience { get; init; } = string.Empty;

    public int ExpirationMinutes { get; init; }
}

public sealed class RefreshTokenOptions
{
    public int ExpirationDays { get; init; }
}
