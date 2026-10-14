using System.Reflection;

namespace Hallagi.Tests;

/// <summary>Reflection helper for reading values out of anonymous objects returned by services.</summary>
public static class Ref
{
    public static object? Val(object? o, string property)
    {
        if (o == null) return null;
        var t = o.GetType();
        var p = t.GetProperty(property, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                ?? t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(x => x.Name.Equals(property, StringComparison.OrdinalIgnoreCase));
        return p?.GetValue(o);
    }

    public static object? Val(object? o, string property, string nested)
        => Val(Val(o, property), nested);

    public static decimal Dec(object? o, string property) => Convert.ToDecimal(Val(o, property));

    public static string Str(object? o, string property) => Convert.ToString(Val(o, property)) ?? string.Empty;

    public static bool Bool(object? o, string property) => Convert.ToBoolean(Val(o, property));

    public static Guid Id(object? o, string property) => (Guid)Val(o, property)!;

    public static int Int(object? o, string property) => Convert.ToInt32(Val(o, property));
}

public static class GuidExt
{
    public static Guid[] AsArray(this Guid g) => new[] { g };
}
