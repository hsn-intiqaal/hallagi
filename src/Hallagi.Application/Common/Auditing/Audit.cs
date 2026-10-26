namespace Hallagi.Application.Common.Auditing;

/// <summary>Small factory for audit rows shared by the newer services.</summary>
public static class Audit
{
    public static AuditLog Entry(CurrentUser user, string action, string entityType, Guid entityId, string? details, DateTime now)
        => new()
        {
            Id = Guid.NewGuid(),
            OrganizationId = user.OrganizationId,
            ActorId = user.ProfileId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId.ToString(),
            Details = details,
            CreatedAt = now
        };
}
