using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Hallagi.Application.Common.Abstractions;
using Hallagi.Domain.Entities;
using Microsoft.IdentityModel.Tokens;

namespace Hallagi.Infrastructure.Identity;

/// <summary>HMAC-SHA256 signed JWT access tokens. Claims carry identity only — permissions are re-read per request.</summary>
public class JwtTokenService(JwtSettings settings) : ITokenService
{
    public int AccessTokenMinutes => settings.ExpiryMinutes;
    public int RefreshTokenDays => settings.RefreshTokenDays;

    public string CreateMemberToken(Profile profile, OrganizationMember member)
        => Write(profile,
            new Claim("member", member.Id.ToString()),
            new Claim("org", member.OrganizationId.ToString()),
            new Claim(ClaimTypes.Role, member.Role.ToString()));

    public string CreateSuperAdminToken(Profile profile)
        => Write(profile,
            new Claim("superadmin", "true"),
            new Claim(ClaimTypes.Role, "SuperAdmin"));

    private string Write(Profile profile, params Claim[] extra)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, profile.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("name", profile.FullName),
            new("email", profile.Email)
        };
        claims.AddRange(extra);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key));
        var token = new JwtSecurityToken(
            issuer: settings.Issuer,
            audience: settings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(settings.ExpiryMinutes),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
