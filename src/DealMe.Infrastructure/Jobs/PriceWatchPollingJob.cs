namespace DealMe.Infrastructure.Jobs;

using DealMe.Core.Domain.Entities;
using DealMe.Core.DTOs;
using DealMe.Core.Interfaces.Notifications;
using DealMe.Infrastructure.Persistence;
using DealMe.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public sealed class PriceWatchPollingJob
{
    private readonly DealMeDbContext _db;
    private readonly ProductSearchService _search;
    private readonly INotificationService _notifications;
    private readonly ILogger<PriceWatchPollingJob> _logger;

    public PriceWatchPollingJob(
        DealMeDbContext db,
        ProductSearchService search,
        INotificationService notifications,
        ILogger<PriceWatchPollingJob> logger)
    {
        _db = db;
        _search = search;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        var watches = await _db.PriceWatches
            .Where(w => w.IsActive)
            .ToListAsync(ct);

        if (watches.Count == 0) return;

        _logger.LogInformation("PriceWatchPolling: checking {Count} active watches", watches.Count);

        var appriseUrls = await _db.NotificationChannels
            .Where(c => c.IsEnabled)
            .Select(c => c.AppriseUrl)
            .ToListAsync(ct);

        foreach (var watch in watches)
        {
            try
            {
                await CheckSingleAsync(watch, appriseUrls, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PriceWatchPolling failed for watch {Id} ({Name})", watch.Id, watch.Name);
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Check a single watch and update its price fields. Caller must call SaveChangesAsync.</summary>
    public async Task CheckSingleAsync(PriceWatch watch, IReadOnlyList<string> appriseUrls, CancellationToken ct)
    {
        var results = await _search.SearchAsync(watch.SearchQuery, ct);
        if (results.Count == 0) return;

        // Results are ranked by relevance (descending). Only compare prices
        // among the top few, and reject results whose price is far below the
        // target — those are accessories, service plans, or wrong products.
        var plausible = results
            .Take(5)
            .Where(r => r.Price >= watch.TargetPrice * 0.25m)
            .ToList();
        if (plausible.Count == 0) return;

        var best = plausible.MinBy(r => r.Price)!;

        var previousPrice = watch.CurrentPrice;
        watch.CurrentPrice     = best.Price;
        watch.CheapestUrl      = best.Url;
        watch.CheapestRetailer = best.Retailer;
        watch.LastCheckedAt    = DateTimeOffset.UtcNow;
        watch.UpdatedAt        = DateTimeOffset.UtcNow;

        // Notify if price dropped to or below target (and wasn't already there)
        var hitTarget = best.Price <= watch.TargetPrice;
        var wasAbove  = previousPrice is null || previousPrice > watch.TargetPrice;

        if (hitTarget && wasAbove && appriseUrls.Count > 0)
        {
            var body = $"{watch.Name}\n" +
                       $"💰 ${best.Price:F2} at {best.Retailer} (target: ${watch.TargetPrice:F2})\n" +
                       $"{best.Url}";

            await _notifications.SendAsync(
                new NotificationPayload($"Price Alert: {watch.Name}", body, appriseUrls), ct);

            _logger.LogInformation(
                "PriceWatch alert sent: {Name} @ ${Price} (target ${Target})",
                watch.Name, best.Price, watch.TargetPrice);
        }
    }
}
