namespace DealMe.Infrastructure.Adapters;

using System.Text.RegularExpressions;

internal static class PriceParser
{
    private static readonly Regex PriceRegex =
        new(@"\$[\d,]+(?:\.\d{1,2})?", RegexOptions.Compiled);

    // "50% off", "save 50%", "50% discount"
    private static readonly Regex PercentOffRegex =
        new(@"(?:save\s+)?(\d{1,3})\s*%\s*off", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // "was $500", "RRP $500", "rrp: $500", "valued at $500"
    private static readonly Regex WasPriceRegex =
        new(@"(?:was|rrp[:\s]?|valued?\s+at|original(?:ly)?)\s*\$?([\d,]+(?:\.\d{1,2})?)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // "now $267", "now: $267"
    private static readonly Regex NowPriceRegex =
        new(@"now[:\s]+\$?([\d,]+(?:\.\d{1,2})?)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // "Free" as a word — used to detect $0 deals (e.g. "Free - Game Name (Was $24.99)")
    private static readonly Regex FreeRegex =
        new(@"\bfree\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    internal static decimal? ExtractFirst(string text)
    {
        var m = PriceRegex.Match(text);
        if (!m.Success) return null;
        return ParseAmount(m.Value.TrimStart('$'));
    }

    internal static (decimal? dealPrice, decimal? originalPrice, int? discountPercent) ExtractDealPricing(string title, string? description)
    {
        var text = description is not null ? $"{title} {description}" : title;

        // 1. Explicit percent off
        int? discountPercent = null;
        var pctMatch = PercentOffRegex.Match(text);
        if (pctMatch.Success && int.TryParse(pctMatch.Groups[1].Value, out var pct) && pct is > 0 and <= 99)
            discountPercent = pct;

        // 2. "Was" / RRP price
        decimal? originalPrice = null;
        var wasMatch = WasPriceRegex.Match(text);
        if (wasMatch.Success)
            originalPrice = ParseAmount(wasMatch.Groups[1].Value);

        // 3. "Now" price (more reliable than generic first-price when was/now both present)
        decimal? dealPrice = null;
        var nowMatch = NowPriceRegex.Match(text);
        if (nowMatch.Success)
            dealPrice = ParseAmount(nowMatch.Groups[1].Value);

        // 4. Detect "Free" deals — if the word "free" appears in the title *before*
        //    any dollar amount, it's a $0 deal. Must run before the first-dollar
        //    fallback below, otherwise we'd grab the "was" price as the deal price
        //    (e.g. "Free - Game (Was $24.99)" → $24.99 instead of $0).
        if (dealPrice is null)
        {
            var freeMatch = FreeRegex.Match(title);
            var firstDollarIdx = title.IndexOf('$');
            if (freeMatch.Success && (firstDollarIdx < 0 || freeMatch.Index < firstDollarIdx))
                dealPrice = 0m;
        }

        // 5. Fall back to first dollar amount in title for deal price
        dealPrice ??= ExtractFirst(title);

        // 6. Calculate discount % from prices if not explicit
        if (discountPercent is null && dealPrice.HasValue && originalPrice.HasValue && originalPrice > 0)
        {
            var saving = originalPrice.Value - dealPrice.Value;
            if (saving > 0)
                discountPercent = (int)Math.Round(saving / originalPrice.Value * 100);
        }

        // 7. Calculate original from deal price + discount % if still missing
        if (originalPrice is null && dealPrice.HasValue && discountPercent.HasValue && discountPercent > 0)
            originalPrice = Math.Round(dealPrice.Value / (1 - discountPercent.Value / 100m), 2);

        return (dealPrice, originalPrice, discountPercent);
    }

    private static decimal? ParseAmount(string s)
        => decimal.TryParse(s.Replace(",", ""), out var d) ? d : null;
}
