namespace Hallagi.Application.Common.Exceptions;

/// <summary>Thrown by services; translated into HTTP responses by the exception middleware.</summary>
public class AppException(int status, string code, string arabicMessage) : Exception(arabicMessage)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
    public string ArabicMessage { get; } = arabicMessage;

    public static AppException Bad(string code, string arabicMessage) => new(400, code, arabicMessage);
    public static AppException Forbidden(string arabicMessage) => new(403, "forbidden", arabicMessage);
    public static AppException NotFound(string arabicMessage) => new(404, "not_found", arabicMessage);
    public static AppException Unauthorized(string arabicMessage) => new(401, "unauthorized", arabicMessage);
    public static AppException Conflict(string code, string arabicMessage) => new(409, code, arabicMessage);
}
