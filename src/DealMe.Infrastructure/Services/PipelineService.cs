namespace DealMe.Infrastructure.Services;

using System.Text.Json;
using DealMe.Core.Domain.Entities;
using DealMe.Core.Domain.Enums;
using DealMe.Core.DTOs;
using DealMe.Core.Interfaces.Notifications;
using DealMe.Core.Interfaces.Pipeline;
using DealMe.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public sealed class PipelineService : IPipelineService
{
    private readonly DealMeDbContext _db;
    private readonly INotificationService _notifications;
    private readonly ILogger<PipelineService> _logger;

    public PipelineService(
        DealMeDbContext db,
        INotificationService notifications,
        ILogger<PipelineService> logger)
    {
        _db = db;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task ProcessAsync(
        IReadOnlyList<NormalisedOpportunity> candidates,
        Guid correlationId,
        CancellationToken ct = default)
    {
        if (candidates.Count == 0) return;

        _logger.LogInformation(
            "Pipeline: {Count} candidates (correlation: {CorrelationId})",
            candidates.Count, correlationId);

        // Load preferences once
        var prefs = await _db.UserPreferences.OrderBy(p => p.CreatedAt).FirstOrDefaultAsync(ct);
        var minValue = prefs?.MinDealValue ?? 5.00m;
        var enabledCategories = ParseJsonArray(prefs?.EnabledCategories);
        var interestTags = ParseJsonArray(prefs?.InterestTags);

        // Load all enabled notification channel URLs once
        var appriseUrls = await _db.NotificationChannels
            .Where(c => c.IsEnabled)
            .Select(c => c.AppriseUrl)
            .ToListAsync(ct);

        // Dedup: load existing SourceIds for the sources in this batch
        var sources = candidates.Select(c => c.Source).Distinct().ToList();
        var sourceIds = candidates.Select(c => c.SourceId).ToList();

        var existing = await _db.Opportunities
            .Where(o => sources.Contains(o.Source) && sourceIds.Contains(o.SourceId))
            .Select(o => new { o.Source, o.SourceId })
            .ToListAsync(ct);

        var existingSet = existing
            .Select(o => $"{o.Source}:{o.SourceId}")
            .ToHashSet(StringComparer.Ordinal);

        var newOpportunities = new List<Opportunity>();

        foreach (var c in candidates)
        {
            // 1. Dedup
            if (existingSet.Contains($"{c.Source}:{c.SourceId}"))
                continue;

            // 2. Filter
            if (!PassesFilter(c, minValue, enabledCategories, interestTags))
                continue;

            // 3. Map to entity
            var opp = new Opportunity
            {
                Id = Guid.NewGuid(),
                Source = c.Source,
                SourceId = c.SourceId,
                Type = c.Type,
                Status = OpportunityStatus.New,
                Title = c.Title,
                Description = c.Description,
                Value = c.Value,
                OriginalValue = c.OriginalValue,
                DiscountPercent = c.DiscountPercent,
                Url = c.Url,
                ImageUrl = c.ImageUrl,
                ExpiresAt = c.ExpiresAt,
                Confidence = c.Confidence,
                Tags = JsonSerializer.Serialize(c.Tags),
                Metadata = JsonSerializer.Serialize(c.Metadata),
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            newOpportunities.Add(opp);
        }

        if (newOpportunities.Count == 0)
        {
            _logger.LogInformation("Pipeline: 0 new opportunities (all dupes or filtered)");
            return;
        }

        // 4. Store opportunities + domain events in one transaction
        _db.Opportunities.AddRange(newOpportunities);

        foreach (var opp in newOpportunities)
        {
            _db.DomainEvents.Add(new DomainEvent
            {
                Id = Guid.NewGuid(),
                EventType = "OpportunityDiscovered",
                AggregateId = opp.Id,
                AggregateType = "Opportunity",
                Payload = JsonSerializer.Serialize(new
                {
                    opp.Source,
                    opp.SourceId,
                    opp.Title,
                    opp.Value,
                    opp.Url
                }),
                Timestamp = DateTimeOffset.UtcNow,
                CorrelationId = correlationId
            });
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Pipeline: stored {Count} new opportunities", newOpportunities.Count);

        // 5. Notify — only if there are channels configured
        if (appriseUrls.Count == 0)
        {
            _logger.LogWarning("Pipeline: no notification channels configured, skipping notifications");
            return;
        }

        foreach (var opp in newOpportunities)
        {
            await _notifications.SendAsync(new NotificationPayload(
                Title: opp.Title,
                Body: BuildNotificationBody(opp),
                AppriseUrls: appriseUrls), ct);
        }
    }

    private static bool PassesFilter(
        NormalisedOpportunity c,
        decimal minValue,
        HashSet<string> enabledCategories,
        HashSet<string> interestTags)
    {
        // Value filter — only apply when a value is present and categories/tags are not restricting
        if (c.Value.HasValue && c.Value < minValue)
            return false;

        // Category filter — skip only if categories are configured AND none match
        if (enabledCategories.Count > 0 &&
            !c.Tags.Any(t => enabledCategories.Contains(t, StringComparer.OrdinalIgnoreCase)))
            return false;

        // Interest tag filter — skip only if interest tags are configured AND none match
        if (interestTags.Count > 0 &&
            !c.Tags.Any(t => interestTags.Contains(t, StringComparer.OrdinalIgnoreCase)) &&
            !interestTags.Any(tag => c.Title.Contains(tag, StringComparison.OrdinalIgnoreCase)))
            return false;

        return true;
    }

    private static string BuildNotificationBody(Opportunity opp)
    {
        var parts = new List<string>();
        if (opp.Value.HasValue)
            parts.Add($"💰 ${opp.Value:F2}");
        if (opp.ExpiresAt.HasValue)
            parts.Add($"⏰ Expires {opp.ExpiresAt.Value.ToLocalTime():ddd d MMM}");
        if (!string.IsNullOrWhiteSpace(opp.Description))
            parts.Add(opp.Description.Length > 200 ? opp.Description[..200] + "…" : opp.Description);
        parts.Add(opp.Url);
        return string.Join("\n", parts);
    }

    private static HashSet<string> ParseJsonArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            var items = JsonSerializer.Deserialize<string[]>(json);
            return items is null ? [] : new HashSet<string>(items, StringComparer.OrdinalIgnoreCase);
        }
        catch { return []; }
    }
}
