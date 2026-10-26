namespace Hallagi.Domain.Entities;

public class MemberBranchAccess
{
    public Guid Id { get; set; }
    public Guid MemberId { get; set; }
    public Guid BranchId { get; set; }
    public OrganizationMember Member { get; set; } = default!;
}
