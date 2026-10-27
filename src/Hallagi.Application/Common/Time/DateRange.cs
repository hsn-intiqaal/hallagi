namespace Hallagi.Application.Common.Time;

/// <summary>
/// An inclusive range of the organization's local calendar days, with the matching
/// half-open UTC window [FromUtc, ToUtc) used to query UTC timestamps.
/// </summary>
public sealed record DateRange(DateOnly From, DateOnly To, DateTime FromUtc, DateTime ToUtc, TimeZoneInfo Zone)
{
    /// <summary>Longest range a report may span (keeps in-memory grouping bounded).</summary>
    public const int MaxDays = 400;

    public static DateRange Create(DateOnly from, DateOnly to, TimeZoneInfo zone)
    {
        if (to < from) (from, to) = (to, from);
        if (to.DayNumber - from.DayNumber >= MaxDays) from = to.AddDays(-(MaxDays - 1));
        return new DateRange(from, to,
            OrgTime.ToUtc(from.ToDateTime(TimeOnly.MinValue), zone),
            OrgTime.ToUtc(to.AddDays(1).ToDateTime(TimeOnly.MinValue), zone),
            zone);
    }

    public int Days => To.DayNumber - From.DayNumber + 1;

    /// <summary>The local calendar day a UTC timestamp belongs to.</summary>
    public DateOnly LocalDay(DateTime utc) => DateOnly.FromDateTime(OrgTime.ToLocal(utc, Zone));

    public IEnumerable<DateOnly> EachDay()
    {
        for (var d = From; d <= To; d = d.AddDays(1)) yield return d;
    }

    /// <summary>The range of the same length immediately before this one.</summary>
    public DateRange Previous() => Create(From.AddDays(-Days), From.AddDays(-1), Zone);
}
