namespace DealMe.Infrastructure.Adapters;

using System.Text.Json;
using DealMe.Core.Domain.Enums;
using DealMe.Core.DTOs;
using DealMe.Core.Interfaces.Adapters;
using Microsoft.Extensions.Logging;

/// <summary>
/// Receives changedetection.io webhook notifications.
/// changedetection.io should be configured to POST to /api/webhooks/changedetection
/// with JSON body. Compatible with the changedetection.io default notification format.
///
/// Expected payload shape (changedetection.io notification variables):
/// {
///   "watch_url": "https://...",       -- URL being monitored
///   "title": "...",                    -- page title
///   "current_snapshot": "...",         -- current page content
///   "uuid": "...",                     -- watch UUID (used as SourceId)
///   "current_price": 99.99             -- optional, if price extraction is configured
/// }
/// </summary>
public sealed class ChangeDetectionAdapter : IIntegrationAdapter
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private readonly ILogger<ChangeDetectionAdapter> _logger;
    private volatile AdapterStatus _lastStatus = new("changedetection", true, null, 0, null);

    public ChangeDetectionAdapter(ILogger<ChangeDetectionAdapter> logger)
    {
        _logger = logger;
    }

    public string AdapterName => "changedetection";

    /// <summary>Webhook-driven — no poll loop.</summary>
    public Task<AdapterResult> FetchAsync(CancellationToken ct = default)
        => Task.FromResult(new AdapterResult(AdapterName, true, null, null, DateTimeOffset.UtcNow));

    public Task<IReadOnlyList<NormalisedOpportunity>> NormaliseAsync(AdapterResult raw, CancellationToken ct = default)
    {
        if (raw.RawPayload is null)
            return Task.FromResult<IReadOnlyList<NormalisedOpportunity>>(Array.Empty<NormalisedOpportunity>());

        try
        {
            using var doc = JsonDocument.Parse(raw.RawPayload);
            var root = doc.RootElement;

            var url = GetString(root, "watch_url", "url") ?? string.Empty;
            var title = GetString(root, "title") ?? url;
            var snapshot = GetString(root, "current_snapshot", "body") ?? string.Empty;
            var uuid = GetString(root, "uuid") ?? Guid.NewGuid().ToString();

            // Price: explicit field first, then extract from snapshot/title
            decimal? value = null;
            if (root.TryGetProperty("current_price", out var priceEl) && priceEl.ValueKind == JsonValueKind.Number)
            {
                value = priceEl.GetDecimal();
            }
            else
            {
                value = PriceParser.ExtractFirst(title) ?? PriceParser.ExtractFirst(snapshot);
            }

            _lastStatus = _lastStatus with
            {
                IsHealthy = true,
                LastRunAt = DateTimeOffset.UtcNow,
                ItemsLastRun = 1,
                LastError = null
            };

            var opportunity = new NormalisedOpportunity(
                Source: AdapterName,
                SourceId: uuid,
                Type: OpportunityType.PriceAlert,
                Title: title,
                Description: snapshot.Length > 500 ? snapshot[..500] : snapshot,
                Value: value,
                OriginalValue: null,
                DiscountPercent: null,
                Url: url,
                ImageUrl: null,
                ExpiresAt: null,
                Confidence: ConfidenceLevel.High,
                Tags: ["price-alert"],
                Metadata: new Dictionary<string, object?>
                {
                    ["uuid"] = uuid,
                    ["snapshot"] = snapshot.Length > 200 ? snapshot[..200] : snapshot
                });

            return Task.FromResult<IReadOnlyList<NormalisedOpportunity>>([opportunity]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ChangeDetectionAdapter failed to parse webhook payload");
            _lastStatus = _lastStatus with { LastError = ex.Message };
            return Task.FromResult<IReadOnlyList<NormalisedOpportunity>>(Array.Empty<NormalisedOpportunity>());
        }
    }

    public Task<bool> HealthCheckAsync(CancellationToken ct = default)
        => Task.FromResult(true);

    public Task<AdapterStatus> GetStatusAsync(CancellationToken ct = default)
        => Task.FromResult(_lastStatus);

    private static string? GetString(JsonElement root, params string[] keys)
    {
        foreach (var key in keys)
            if (root.TryGetProperty(key, out var el) && el.ValueKind == JsonValueKind.String)
                return el.GetString();
        return null;
    }
}
