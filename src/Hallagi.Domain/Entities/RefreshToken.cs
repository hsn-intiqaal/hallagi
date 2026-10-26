namespace Hallagi.Domain.Entities;

/// <summary>Long-lived refresh token (only its SHA-256 hash is stored). Rotated on every use.</summary>
public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid ProfileId { get; set; }
    public Guid? MemberId { get; set; }
    public string TokenHash { get; set; } = default!;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? ReplacedByHash { get; set; }
}
