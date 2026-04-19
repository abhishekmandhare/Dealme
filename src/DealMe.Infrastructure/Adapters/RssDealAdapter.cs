namespace DealMe.Infrastructure.Adapters;

using System.Text.RegularExpressions;
using System.Xml.Linq;
using DealMe.Core.Domain.Enums;
using DealMe.Core.DTOs;
using DealMe.Core.Interfaces.Adapters;
using Microsoft.Extensions.Logging;

/// <summary>
/// Shared base for RSS deal feeds that follow the OzBargain/Cheapies structure
/// (ozb:meta element, guid-based SourceId, category tags).
/// </summary>
public abstract class RssDealAdapter : IIntegrationAdapter
{
    private static readonly XNamespace OzbNs = "https://www.ozbargain.com.au";
    private static readonly Regex HtmlTagRegex = new(@"<[^>]+>", RegexOptions.Compiled);

    protected readonly HttpClient Http;
    protected readonly ILogger Logger;

    private volatile AdapterStatus _lastStatus;

    protected RssDealAdapter(HttpClient http, ILogger logger)
    {
        Http = http;
        Logger = logger;
        _lastStatus = new AdapterStatus(AdapterName, false, null, 0, null);
    }

    public abstract string AdapterName { get; }
    protected abstract string FeedUrl { get; }

    /// <summary>
    /// Override to classify this feed's items as a specific OpportunityType.
    /// Defaults to Deal (with Cashback detection from title/categories).
    /// </summary>
    protected virtual OpportunityType DefaultType => OpportunityType.Deal;

    public async Task<AdapterResult> FetchAsync(CancellationToken ct = default)
    {
        try
        {
            var xml = await Http.GetStringAsync(FeedUrl, ct);
            _lastStatus = _lastStatus with { IsHealthy = true, LastRunAt = DateTimeOffset.UtcNow, LastError = null };
            return new AdapterResult(AdapterName, true, xml, null, DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Adapter {Adapter} fetch failed", AdapterName);
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

        // Determine which ozb namespace this feed uses
        var ozbNs = doc.Root?.Attribute(XNamespace.Xmlns + "ozb") is { } attr
            ? (XNamespace)attr.Value
            : OzbNs;

        var results = new List<NormalisedOpportunity>();

        foreach (var item in items)
        {
            try
            {
                results.Add(ParseItem(item, ozbNs));
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Adapter {Adapter} failed to parse item: {Title}",
                    AdapterName, item.Element("title")?.Value ?? "?");
            }
        }

        _lastStatus = _lastStatus with { ItemsLastRun = results.Count };
        Logger.LogInformation("Adapter {Adapter} parsed {Count} items", AdapterName, results.Count);

        return Task.FromResult<IReadOnlyList<NormalisedOpportunity>>(results);
    }

    private NormalisedOpportunity ParseItem(XElement item, XNamespace ozbNs)
    {
        var title = item.Element("title")?.Value ?? string.Empty;
        var link = item.Element("link")?.Value ?? string.Empty;

        var meta = item.Element(ozbNs + "meta");
        var dealUrl = meta?.Attribute("url")?.Value ?? link;
        var imageUrl = meta?.Attribute("image")?.Value;
        var expiryStr = meta?.Attribute("expiry")?.Value;

        DateTimeOffset? expiresAt = expiryStr is not null && DateTimeOffset.TryParse(expiryStr, out var exp)
            ? exp.ToUniversalTime()
            : null;

        // SourceId from guid: "954683 at https://www.ozbargain.com.au"
        var guid = item.Element("guid")?.Value ?? string.Empty;
        var sourceId = guid.Contains(' ') ? guid[..guid.IndexOf(' ')] : guid;

        // Categories/tags
        var categories = item.Elements("category")
            .Select(c => c.Value.Trim())
            .Where(c => !string.IsNullOrEmpty(c))
            .ToArray();

        // Description — strip HTML
        var descRaw = item.Element("description")?.Value ?? string.Empty;
        var description = HtmlTagRegex.Replace(descRaw, "").Trim();
        if (string.IsNullOrWhiteSpace(description)) description = null;

        // Pricing — extract deal price, RRP, and discount %
        var (value, originalValue, discountPercent) = PriceParser.ExtractDealPricing(title, description);

        // Type — subclass DefaultType wins (e.g. Competitions feed), otherwise detect Cashback.
        OpportunityType type;
        if (DefaultType != OpportunityType.Deal)
        {
            type = DefaultType;
        }
        else
        {
            type = categories.Any(c => c.Equals("Cashback", StringComparison.OrdinalIgnoreCase))
                   || title.Contains("cashback", StringComparison.OrdinalIgnoreCase)
                ? OpportunityType.Cashback
                : OpportunityType.Deal;
        }

        // pubDate → posted time (not expiry)
        var pubDate = item.Element("pubDate")?.Value;

        return new NormalisedOpportunity(
            Source: AdapterName,
            SourceId: sourceId,
            Type: type,
            Title: title,
            Description: description,
            Value: value,
            OriginalValue: originalValue,
            DiscountPercent: discountPercent,
            Url: dealUrl,
            ImageUrl: imageUrl,
            ExpiresAt: expiresAt,
            Confidence: ConfidenceLevel.High,
            Tags: categories,
            Metadata: new Dictionary<string, object?>
            {
                ["dealPage"] = link,
                ["pubDate"] = pubDate,
                ["votesPos"] = meta?.Attribute("votes-pos")?.Value,
                ["votesNeg"] = meta?.Attribute("votes-neg")?.Value,
                ["clickCount"] = meta?.Attribute("click-count")?.Value,
            });
    }

    public async Task<bool> HealthCheckAsync(CancellationToken ct = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Head, FeedUrl);
            using var resp = await Http.SendAsync(req, ct);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public Task<AdapterStatus> GetStatusAsync(CancellationToken ct = default)
        => Task.FromResult(_lastStatus);
}
