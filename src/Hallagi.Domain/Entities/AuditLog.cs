namespace Hallagi.Domain.Entities;

public class AuditLog
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid ActorId { get; set; }
    public string Action { get; set; } = default!;
    public string EntityType { get; set; } = default!;
    public string EntityId { get; set; } = default!;
    public string? Details { get; set; }
    public DateTime CreatedAt { get; set; }
}
