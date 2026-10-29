namespace Hallagi.Application.Common.Time;

/// <summary>
/// "Now" and "today" as the organization experiences them. Timestamps are stored in UTC;
/// every calendar decision (today, report days, defaults) is made in the organization's time zone.
/// </summary>
public sealed class OrgClock(IAppDbContext db, TimeProvider time)
{
    private readonly Dictionary<Guid, TimeZoneInfo> _zones = new();

    public DateTime UtcNow => time.GetUtcNow().UtcDateTime;

    public async Task<TimeZoneInfo> ZoneAsync(Guid orgId, CancellationToken ct = default)
    {
        if (_zones.TryGetValue(orgId, out var cached)) return cached;
        var id = await db.Organizations.AsNoTracking().Where(o => o.Id == orgId).Select(o => o.TimeZone).FirstOrDefaultAsync(ct);
        return _zones[orgId] = OrgTime.Resolve(id);
    }

    public async Task<DateOnly> TodayAsync(Guid orgId, CancellationToken ct = default)
        => DateOnly.FromDateTime(OrgTime.ToLocal(UtcNow, await ZoneAsync(orgId, ct)));

    /// <summary>
    /// Builds an inclusive local-day range from optional query values (only their date part is used).
    /// Missing bounds default to the last <paramref name="defaultDays"/> days ending today.
    /// </summary>
    public async Task<DateRange> RangeAsync(Guid orgId, DateTime? from, DateTime? to, int defaultDays, CancellationToken ct = default)
    {
        var zone = await ZoneAsync(orgId, ct);
        var today = DateOnly.FromDateTime(OrgTime.ToLocal(UtcNow, zone));
        var end = to is null ? today : DateOnly.FromDateTime(to.Value);
        var start = from is null ? end.AddDays(-(defaultDays - 1)) : DateOnly.FromDateTime(from.Value);
        return DateRange.Create(start, end, zone);
    }
}
