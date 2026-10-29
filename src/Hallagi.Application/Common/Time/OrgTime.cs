namespace Hallagi.Application.Common.Time;

/// <summary>Converts between UTC (storage) and the organization's local clock (Asia/Riyadh by default).</summary>
public static class OrgTime
{
    public static TimeZoneInfo Resolve(string? timeZoneId)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(string.IsNullOrWhiteSpace(timeZoneId) ? "Asia/Riyadh" : timeZoneId); }
        catch (TimeZoneNotFoundException)
        {
            // Containers without tzdata: Saudi Arabia is UTC+3 with no daylight saving.
            return TimeZoneInfo.CreateCustomTimeZone("Asia/Riyadh", TimeSpan.FromHours(3), "Asia/Riyadh", "Asia/Riyadh");
        }
    }

    public static DateTime ToLocal(DateTime utc, TimeZoneInfo tz)
        => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), tz);

    public static DateTime ToUtc(DateTime local, TimeZoneInfo tz)
        => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), tz);

    public static DateOnly Today(TimeZoneInfo tz) => DateOnly.FromDateTime(ToLocal(DateTime.UtcNow, tz));
}
