namespace Hallagi.Application.Common.Security;

/// <summary>Resolves the authenticated user for the current request (implemented by the web host).</summary>
public interface ICurrentUserAccessor
{
    CurrentUser User { get; }
    bool IsAuthenticated { get; }
}
