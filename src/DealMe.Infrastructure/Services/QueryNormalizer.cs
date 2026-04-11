namespace DealMe.Infrastructure.Services;

using System.Text.RegularExpressions;

/// <summary>
/// Normalizes product search queries for better matching.
/// Collapses common spacing issues: "64 gb" → "64gb", "ddr 5" → "ddr5", etc.
/// </summary>
internal static partial class QueryNormalizer
{
    public static string Normalize(string query)
    {
        var q = query.Trim();

        // "64 gb" / "64 GB" / "64 Gb" → "64gb"
        q = SizeSpaceRegex().Replace(q, m => m.Value.Replace(" ", "").ToLowerInvariant());

        // "ddr 5" / "DDR 4" → "ddr5" / "ddr4"
        q = DdrSpaceRegex().Replace(q, m => m.Value.Replace(" ", "").ToLowerInvariant());

        // "wi fi" / "Wi Fi" → "wifi"
        q = WiFiRegex().Replace(q, "wifi");

        // "mac book" → "macbook"
        q = MacBookRegex().Replace(q, "macbook");

        // Collapse multiple spaces
        q = MultiSpaceRegex().Replace(q, " ");

        return q.Trim();
    }

    [GeneratedRegex(@"\b(\d+)\s+(gb|tb|mb)\b", RegexOptions.IgnoreCase)]
    private static partial Regex SizeSpaceRegex();

    [GeneratedRegex(@"\b(ddr|lp ?ddr)\s*(\d)\b", RegexOptions.IgnoreCase)]
    private static partial Regex DdrSpaceRegex();

    [GeneratedRegex(@"\bwi\s*fi\b", RegexOptions.IgnoreCase)]
    private static partial Regex WiFiRegex();

    [GeneratedRegex(@"\bmac\s+book\b", RegexOptions.IgnoreCase)]
    private static partial Regex MacBookRegex();

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex MultiSpaceRegex();
}
