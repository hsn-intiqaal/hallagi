using System.Text.Json;
using System.Text.RegularExpressions;

namespace Hallagi.Application.Organizations;

/// <summary>Validation + normalization for tenant branding (colors and images).</summary>
public static class Branding
{
    private static readonly Regex HexColor = new("^#([0-9a-fA-F]{6})$", RegexOptions.Compiled);
    public const int MaxImageChars = 1_500_000;

    /// <summary>Returns a valid #RRGGBB color, or the fallback when the value is missing/invalid.</summary>
    public static string NormalizeColor(string? value, string fallback)
        => !string.IsNullOrWhiteSpace(value) && HexColor.IsMatch(value.Trim()) ? value.Trim() : fallback;

    /// <summary>Accepts only inline image data URLs within the size cap; everything else is dropped.</summary>
    public static string? NormalizeImage(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value.Trim();
        if (v.Length > MaxImageChars)
            throw AppException.Bad("image_too_large", "حجم الصورة كبير جدًا (الحد الأقصى 1.5 ميجابايت)");
        return v.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase) ? v : null;
    }
}
