namespace DealMe.Infrastructure.Adapters;

using System.Text.RegularExpressions;
using System.Xml.Linq;
using DealMe.Core.Domain.Enums;
using DealMe.Core.DTOs;
using DealMe.Core.Interfaces.Adapters;
using Microsoft.Extensions.Logging;

/// <summary>
/// Fetches credit card deals from PointHacks (WordPress RSS feed).
/// Different structure from OzBargain — no ozb:meta, standard RSS with dc:creator.
/// </summary>
public sealed partial class PointHacksAdapter : IIntegrationAdapter
{
    private const string FeedUrl = "https://www.pointhacks.com.au/category/credit-cards/feed/";

    private static readonly XNamespace DcNs = "http://purl.org/dc/elements/1.1/";
    private static readonly XNamespace ContentNs = "http://purl.org/rss/1.0/modules/content/";

    private readonly HttpClient _http;
    private readonly ILogger<PointHacksAdapter> _logger;
    private volatile AdapterStatus _lastStatus;

    public PointHacksAdapter(HttpClient http, ILogger<PointHacksAdapter> logger)
    {
        _http = http;
        _logger = logger;
        _lastStatus = new AdapterStatus(AdapterName, false, null, 0, null);
    }

    public string AdapterName => "pointhacks";

    public async Task<AdapterResult> FetchAsync(CancellationToken ct = default)
    {
        try
        {
            var xml = await _http.GetStringAsync(FeedUrl, ct);
            _lastStatus = _lastStatus with { IsHealthy = true, LastRunAt = DateTimeOffset.UtcNow, LastError = null };
            return new AdapterResult(AdapterName, true, xml, null, DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Adapter {Adapter} fetch failed", AdapterName);
            _lastStatus = _lastStatus with { IsHealthy = false, LastRunAt = DateTimeOffset.UtcNow, LastError = ex.Message };
            return new AdapterResult(AdapterName, false, null, ex.Message, DateTimeOffset.UtcNow);
        }
    }

    public Task<IReadOnlyList<NormalisedOpportunity>> NormaliseAsync(AdapterResult raw, CancellationToken ct = default)
    {
        if (!raw.Success || raw.RawPayload is null)
            return Task.FromResult<IReadOnlyList<NormalisedOpportunity>>(Array.Empty<NormalisedOpportunity>());

        var doc = XDocument.Parse(raw.RawPayload);
        var items = doc.Descendants("item");
        var results = new List<NormalisedOpportunity>();

        foreach (var item in items)
        {
            try
            {
                results.Add(ParseItem(item));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Adapter {Adapter} failed to parse item: {Title}",
                    AdapterName, item.Element("title")?.Value ?? "?");
            }
        }

        _lastStatus = _lastStatus with { ItemsLastRun = results.Count };
        _logger.LogInformation("Adapter {Adapter} parsed {Count} items", AdapterName, results.Count);

        return Task.FromResult<IReadOnlyList<NormalisedOpportunity>>(results);
    }

    private NormalisedOpportunity ParseItem(XElement item)
    {
        var title = item.Element("title")?.Value ?? string.Empty;
        var link = item.Element("link")?.Value ?? string.Empty;
        var pubDate = item.Element("pubDate")?.Value;
        var author = item.Element(DcNs + "creator")?.Value;

        // SourceId from guid (WordPress guids are URLs like "https://www.pointhacks.com.au/?p=12345")
        var guid = item.Element("guid")?.Value ?? link;
        var sourceId = ExtractWordPressId(guid);

        // Categories/tags
        var categories = item.Elements("category")
            .Select(c => c.Value.Trim())
            .Where(c => !string.IsNullOrEmpty(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        // Description — strip HTML from the excerpt
        var descRaw = item.Element("description")?.Value ?? string.Empty;
        var description = HtmlTagRegex().Replace(descRaw, "").Trim();
        if (string.IsNullOrWhiteSpace(description)) description = null;

        // Determine type: cashback mentions → Cashback, otherwise Bonus (sign-up bonuses, points)
        var type = title.Contains("cashback", StringComparison.OrdinalIgnoreCase)
                   || categories.Any(c => c.Contains("cashback", StringComparison.OrdinalIgnoreCase))
            ? OpportunityType.Cashback
            : OpportunityType.Bonus;

        // Ensure "Credit Cards" tag is present for category mapping
        var tagList = categories.ToList();
        if (!tagList.Any(t => t.Equals("Credit Cards", StringComparison.OrdinalIgnoreCase)))
            tagList.Insert(0, "Credit Cards");

        return new NormalisedOpportunity(
            Source: AdapterName,
            SourceId: sourceId,
            Type: type,
            Title: title,
            Description: description,
            Value: null,
            OriginalValue: null,
            DiscountPercent: null,
            Url: link,
            ImageUrl: null,
            ExpiresAt: null,
            Confidence: ConfidenceLevel.Medium,
            Tags: tagList.ToArray(),
            Metadata: new Dictionary<string, object?>
            {
                ["pubDate"] = pubDate,
                ["author"] = author,
                ["wpCategories"] = categories,
            });
    }

    /// <summary>
    /// Extracts a stable ID from a WordPress guid URL like "https://www.pointhacks.com.au/?p=12345".
    /// Falls back to hashing the full URL if the pattern doesn't match.
    /// </summary>
    private static string ExtractWordPressId(string guid)
    {
        var match = WpIdRegex().Match(guid);
        return match.Success ? match.Groups[1].Value : guid.GetHashCode().ToString("x8");
    }

    public async Task<bool> HealthCheckAsync(CancellationToken ct = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Head, FeedUrl);
            using var resp = await _http.SendAsync(req, ct);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public Task<AdapterStatus> GetStatusAsync(CancellationToken ct = default)
        => Task.FromResult(_lastStatus);

    [GeneratedRegex(@"<[^>]+>", RegexOptions.Compiled)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"[?&]p=(\d+)")]
    private static partial Regex WpIdRegex();
}
