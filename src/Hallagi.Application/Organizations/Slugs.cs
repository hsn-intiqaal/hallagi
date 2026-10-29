namespace Hallagi.Application.Organizations;

public static class Slugs
{
    private static readonly HashSet<string> Reserved = new() { "admin", "api", "app", "book", "login", "platform", "hallagi", "www" };

    /// <summary>Lower-case latin letters, digits and dashes (3–40 chars).</summary>
    public static string Normalize(string raw)
    {
        var s = raw.Trim().ToLowerInvariant();
        if (s.Length is < 3 or > 40 || !s.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-')
            || s.StartsWith('-') || s.EndsWith('-') || Reserved.Contains(s))
            throw AppException.Bad("invalid_slug", "رابط الحجز: من 3 إلى 40 حرفًا إنجليزيًا صغيرًا أو رقمًا أو شرطة (-)");
        return s;
    }

    public static async Task<string> RandomUniqueAsync(IAppDbContext db, CancellationToken ct)
    {
        const string alphabet = "abcdefghjkmnpqrstuvwxyz23456789";
        while (true)
        {
            var slug = "salon-" + new string(Enumerable.Range(0, 6)
                .Select(_ => alphabet[System.Security.Cryptography.RandomNumberGenerator.GetInt32(alphabet.Length)]).ToArray());
            if (!await db.Organizations.AnyAsync(o => o.Slug == slug, ct)) return slug;
        }
    }
}
