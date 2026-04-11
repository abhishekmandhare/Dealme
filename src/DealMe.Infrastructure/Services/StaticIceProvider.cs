namespace DealMe.Infrastructure.Services;

using AngleSharp;
using AngleSharp.Dom;
using DealMe.Core.DTOs;
using DealMe.Core.Interfaces;
using Microsoft.Extensions.Logging;

public sealed class StaticIceProvider : IProductSearchProvider
{
    private readonly HttpClient _http;
    private readonly ILogger<StaticIceProvider> _logger;

    private static readonly IBrowsingContext Browser =
        BrowsingContext.New(Configuration.Default);

    public string Name => "StaticIce";

    public StaticIceProvider(HttpClient http, ILogger<StaticIceProvider> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ProductSearchResult>> SearchAsync(string query, CancellationToken ct = default)
    {
        var normalized = QueryNormalizer.Normalize(query);
        var url = $"https://www.staticice.com.au/cgi-bin/search.cgi?q={Uri.EscapeDataString(normalized)}&stype=1&links=50";

        string html;
        try
        {
            html = await _http.GetStringAsync(url, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "StaticIce search failed for '{Query}' (normalized: '{Normalized}')", query, normalized);
            return [];
        }

        var doc = await Browser.OpenAsync(req => req.Content(html), ct);
        var results = new List<ProductSearchResult>();

        var rows = doc.QuerySelectorAll("tr[valign='top']");

        foreach (var row in rows)
        {
            var cells = row.QuerySelectorAll("td");
            if (cells.Length < 2) continue;

            var priceCell = cells[0];
            var descCell  = cells[1];

            var priceLink = priceCell.QuerySelector("a");
            if (priceLink is null) continue;
            var priceText = priceLink.TextContent.Trim().TrimStart('$').Replace(",", "");
            if (!decimal.TryParse(priceText, out var price)) continue;

            var href = priceLink.GetAttribute("href") ?? string.Empty;
            var retailerUrl = ExtractNewUrl(href);
            if (string.IsNullOrWhiteSpace(retailerUrl)) continue;

            var name = descCell.ChildNodes
                .OfType<IText>()
                .Select(t => t.TextContent.Trim())
                .FirstOrDefault(t => t.Length > 0);
            if (string.IsNullOrWhiteSpace(name)) continue;

            var retailerName = descCell.QuerySelector("font a")?.TextContent.Trim() ?? "Unknown";

            results.Add(new ProductSearchResult(name, retailerName, price, retailerUrl));
        }

        // Score and rank results
        var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var maxPrice = results.Count > 0 ? (double)results.Max(r => r.Price) : 0;

        var ranked = results
            .OrderByDescending(r => Score(r.Name, words, (double)r.Price, maxPrice))
            .ToList();

        _logger.LogInformation("StaticIce: '{Query}' (normalized: '{Normalized}') → {Count} results", query, normalized, ranked.Count);
        return ranked;
    }

    private static readonly string[] AccessoryKeywords =
    [
        "case", "cover", "sleeve", "bag", "pouch", "skin", "wrap", "decal", "sticker",
        "stand", "mount", "holder", "dock", "hub", "dongle", "adapter", "adaptor",
        "cable", "cord", "charger", "charging", "power bank",
        "screen protector", "tempered glass", "protector",
        "strap", "band", "clip", "hook", "hinge",
        "keyboard cover", "palm rest", "wrist rest",
        "cleaning", "cloth",
        "stylus", "pen tip",
        "replacement", "spare", "repair", "part",
        "warranty", "insurance", "applecare", "care plan", "protection plan", "extended plan",
        "grip", "shell", "bumper", "wallet",
        "bracket", "riser", "tray",
        "filter", "lens cap", "lens hood",
        "earbud tip", "ear tip", "ear pad", "ear cushion",
        "foam", "padding",
        "screwdriver", "screw driver", "spudger", "pry", "opening tool",
        "rubber feet", "rubber foot", "foot pad", "feet set",
        "battery replacement", "battery kit",
        "heat gun", "suction cup",
    ];

    private static int Score(string name, string[] queryWords, double price, double maxPrice)
    {
        var nameLower = name.ToLowerInvariant();

        var wordScore = queryWords.Count(w => nameLower.Contains(w.ToLowerInvariant())) * 3;
        var phraseBonus = nameLower.Contains(string.Join(" ", queryWords).ToLowerInvariant()) ? 15 : 0;
        var accessoryPenalty = AccessoryKeywords.Count(k => nameLower.Contains(k)) * -15;
        var absoluteFloorPenalty = price < 20 ? -40 : price < 50 ? -10 : 0;
        var priceRatio = maxPrice > 0 ? price / maxPrice : 1.0;
        var relativePenalty = priceRatio < 0.15 ? -25 : priceRatio < 0.30 ? -10 : 0;

        return wordScore + phraseBonus + accessoryPenalty + absoluteFloorPenalty + relativePenalty;
    }

    private static string ExtractNewUrl(string href)
    {
        var idx = href.IndexOf("newurl=", StringComparison.Ordinal);
        if (idx < 0) return string.Empty;
        var encoded = href[(idx + 7)..];
        var end = encoded.IndexOf('&');
        if (end >= 0) encoded = encoded[..end];
        return Uri.UnescapeDataString(encoded);
    }
}
