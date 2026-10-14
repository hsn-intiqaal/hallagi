namespace Hallagi.Api.Services;

/// <summary>Thrown by services; translated into HTTP responses by the exception middleware.</summary>
public class ApiException(int status, string code, string arabicMessage) : Exception(arabicMessage)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
    public string ArabicMessage { get; } = arabicMessage;

    public static ApiException Bad(string code, string arabicMessage) => new(400, code, arabicMessage);
    public static ApiException Forbidden(string arabicMessage) => new(403, "forbidden", arabicMessage);
    public static ApiException NotFound(string arabicMessage) => new(404, "not_found", arabicMessage);
    public static ApiException Unauthorized(string arabicMessage) => new(401, "unauthorized", arabicMessage);
    public static ApiException Conflict(string code, string arabicMessage) => new(409, code, arabicMessage);
}
