namespace Hallagi.Infrastructure.Persistence.Configurations;

/// <summary>Financial precision: fixed-point decimals everywhere, never floating point.</summary>
internal static class ColumnTypes
{
    public const string Money = "decimal(10,2)";
    public const string Rate = "decimal(8,4)";
}
