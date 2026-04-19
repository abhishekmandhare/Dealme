namespace DealMe.Infrastructure.Jobs;

using System.Text.Json;
using DealMe.Core.Domain.Entities;
using DealMe.Core.Domain.Enums;
using DealMe.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

/// <summary>
/// Scheduled batch: finds recent competition opportunities that we haven't
/// attempted yet, calls the automation service to auto-enter each one, and
/// records the outcome in CompetitionEntries.
///
/// Rate-limited to MaxPerRun attempts per invocation to avoid looking like
/// a scraper to comp hosts.
/// </summary>
public sealed class CompetitionEntryJob
{
    private readonly DealMeDbContext _db;
    private readonly HttpClient _http;
    private readonly string _automationUrl;
    private readonly string? _email;
    private readonly string? _firstName;
    private readonly string? _lastName;
    private readonly string? _postcode;
    private readonly int _maxPerRun;
    private readonly ILogger<CompetitionEntryJob> _logger;

    public CompetitionEntryJob(
        DealMeDbContext db,
        IHttpClientFactory httpFactory,
        IConfiguration config,
        ILogger<CompetitionEntryJob> logger)
    {
        _db = db;
        _http = httpFactory.CreateClient("automation");
        _automationUrl = (config["AUTOMATION_URL"] ?? "http://automation:3100").TrimEnd('/');
        _email = config["COMPETITION_EMAIL"];
        _firstName = config["COMPETITION_FIRST_NAME"];
        _lastName = config["COMPETITION_LAST_NAME"];
        _postcode = config["COMPETITION_POSTCODE"];
        _maxPerRun = int.TryParse(config["COMPETITION_MAX_PER_RUN"], out var n) ? n : 5;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_email))
        {
            _logger.LogWarning("CompetitionEntryJob: COMPETITION_EMAIL not set, skipping run");
            return;
        }

        // Client-side filter on expiry + attempted-ids: EF can't translate the combined
        // expression against string-stored DateTimeOffsets with a NOT-IN list.
        var now = DateTimeOffset.UtcNow;
        var cutoff = now.AddDays(-14);
        var attemptedIds = (await _db.CompetitionEntries.Select(e => e.OpportunityId).ToListAsync(ct))
            .ToHashSet();

        var rows = await _db.Opportunities
            .Where(o => o.Type == OpportunityType.Competition && o.CreatedAt >= cutoff)
            .OrderByDescending(o => o.CreatedAt)
            .Take(_maxPerRun * 4)
            .ToListAsync(ct);

        var candidates = rows
            .Where(o => !attemptedIds.Contains(o.Id))
            .Where(o => o.ExpiresAt == null || o.ExpiresAt > now)
            .Take(_maxPerRun)
            .ToList();

        _logger.LogInformation("CompetitionEntryJob: {Count} candidates (max/run={Max})",
            candidates.Count, _maxPerRun);

        foreach (var opp in candidates)
        {
            await TryEnterAsync(opp, ct);
        }
    }

    private async Task TryEnterAsync(Opportunity opp, CancellationToken ct)
    {
        var entry = new CompetitionEntry
        {
            Id = Guid.NewGuid(),
            OpportunityId = opp.Id,
            Title = opp.Title,
            CompetitionUrl = opp.Url,
            SourceUrl = opp.Url,
            Source = opp.Source,
            Status = EntryStatus.Pending,
            EmailUsed = _email,
            ExpiresAt = opp.ExpiresAt,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                url = opp.Url,
                email = _email,
                firstName = _firstName,
                lastName = _lastName,
                postcode = _postcode,
            });
            var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
            var resp = await _http.PostAsync($"{_automationUrl}/run/enter-competition", content, ct);
            var json = await resp.Content.ReadAsStringAsync(ct);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var status = root.TryGetProperty("status", out var s) ? s.GetString() : "failed";
            var reason = root.TryGetProperty("reason", out var r) && r.ValueKind == JsonValueKind.String
                ? r.GetString() : null;

            entry.Status = status switch
            {
                "entered" => EntryStatus.Entered,
                "skipped" => EntryStatus.Skipped,
                _ => EntryStatus.Failed,
            };
            entry.Reason = reason;
            entry.AttemptedAt = DateTimeOffset.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CompetitionEntryJob: entry failed for {Url}", opp.Url);
            entry.Status = EntryStatus.Failed;
            entry.Reason = ex.Message;
            entry.AttemptedAt = DateTimeOffset.UtcNow;
        }

        _db.CompetitionEntries.Add(entry);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("CompetitionEntryJob: {Status} {Title} ({Reason})",
            entry.Status, opp.Title, entry.Reason ?? "—");
    }
}
