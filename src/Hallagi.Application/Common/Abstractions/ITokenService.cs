namespace Hallagi.Application.Common.Abstractions;

/// <summary>Issues signed access tokens; the signing technology lives in Infrastructure.</summary>
public interface ITokenService
{
    int AccessTokenMinutes { get; }
    int RefreshTokenDays { get; }
    string CreateMemberToken(Profile profile, OrganizationMember member);
    string CreateSuperAdminToken(Profile profile);
}
