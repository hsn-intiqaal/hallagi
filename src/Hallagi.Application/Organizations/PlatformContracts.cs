using System.Text.Json;
using System.Text.RegularExpressions;

namespace Hallagi.Application.Organizations;

public record CreateOrganizationRequest(
    string Name,
    string City,
    string? BranchName,
    string? Address,
    string OwnerName,
    string OwnerEmail,
    string? OwnerPhone,
    string OwnerPassword,
    Guid? PlanId,
    string? LogoUrl = null,
    string? CoverImageUrl = null,
    string? PrimaryColor = null,
    string? SecondaryColor = null);
