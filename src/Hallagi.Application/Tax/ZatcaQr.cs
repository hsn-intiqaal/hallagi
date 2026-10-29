using System.Globalization;
using System.Text;

namespace Hallagi.Application.Tax;

/// <summary>
/// ZATCA e-invoicing phase 1 QR payload: TLV (tag, length, UTF-8 value) encoded as Base64.
/// Tags: 1 seller name, 2 VAT number, 3 timestamp (ISO 8601), 4 invoice total incl. VAT, 5 VAT total.
/// </summary>
public static class ZatcaQr
{
    public static string Encode(string sellerName, string vatNumber, DateTime timestampUtc, decimal totalWithVat, decimal vatTotal)
    {
        using var ms = new MemoryStream();
        Write(ms, 1, sellerName);
        Write(ms, 2, vatNumber);
        Write(ms, 3, DateTime.SpecifyKind(timestampUtc, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
        Write(ms, 4, totalWithVat.ToString("0.00", CultureInfo.InvariantCulture));
        Write(ms, 5, vatTotal.ToString("0.00", CultureInfo.InvariantCulture));
        return Convert.ToBase64String(ms.ToArray());
    }

    /// <summary>Decodes a TLV payload back into tag → value (used by tests and diagnostics).</summary>
    public static Dictionary<int, string> Decode(string base64)
    {
        var bytes = Convert.FromBase64String(base64);
        var result = new Dictionary<int, string>();
        var i = 0;
        while (i + 2 <= bytes.Length)
        {
            int tag = bytes[i], len = bytes[i + 1];
            result[tag] = Encoding.UTF8.GetString(bytes, i + 2, len);
            i += 2 + len;
        }
        return result;
    }

    private static void Write(Stream s, byte tag, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        if (bytes.Length > 255) throw new ArgumentException($"TLV value for tag {tag} exceeds 255 bytes");
        s.WriteByte(tag);
        s.WriteByte((byte)bytes.Length);
        s.Write(bytes);
    }
}
