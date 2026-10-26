namespace Hallagi.Infrastructure.Identity;

public class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = "Hallagi";
    public string Audience { get; set; } = "Hallagi";
    public int ExpiryMinutes { get; set; } = 720;
    /// <summary>Lifetime of rotating refresh tokens.</summary>
    public int RefreshTokenDays { get; set; } = 30;
}
