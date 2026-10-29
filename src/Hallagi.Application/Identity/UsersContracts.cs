using System.Text.Json;

namespace Hallagi.Application.Identity;

public record InviteUserRequest(string FullName, string Email, string? Phone, string Password, UserRole Role, List<Guid> BranchIds, Dictionary<string, bool>? Permissions);

public record UpdateUserRequest(string? FullName, UserRole Role, MemberStatus Status, List<Guid> BranchIds, Dictionary<string, bool>? Permissions);
