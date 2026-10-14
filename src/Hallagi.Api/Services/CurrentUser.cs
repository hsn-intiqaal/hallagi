using Hallagi.Api.Domain;

namespace Hallagi.Api.Services;

/// <summary>
/// Authenticated user context, built from verified JWT claims only.
/// Organization and branch membership are always server-derived.
/// </summary>
public record CurrentUser(
    Guid ProfileId,
    Guid OrganizationId,
    Guid MemberId,
    UserRole Role,
    bool MemberActive,
    IReadOnlyList<Guid> BranchIds,
    Dictionary<string, bool>? PermissionOverrides);
