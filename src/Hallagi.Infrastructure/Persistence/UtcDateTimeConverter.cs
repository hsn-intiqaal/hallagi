using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Hallagi.Infrastructure.Persistence;

/// <summary>
/// All timestamps are stored as UTC. Databases drop DateTimeKind, so values come back "Unspecified"
/// and would be serialized without a zone (browsers then read them as local time).
/// This converter restores Kind=Utc on read so the API always emits ISO-8601 with "Z".
/// </summary>
public sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    v => v.Kind == DateTimeKind.Local ? v.ToUniversalTime() : DateTime.SpecifyKind(v, DateTimeKind.Utc),
    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
