namespace DealMe.Api.Controllers;

using DealMe.Core.Domain.Entities;
using DealMe.Core.Domain.Enums;
using DealMe.Infrastructure.Jobs;
using DealMe.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/[controller]")]
public class CompetitionsController : ControllerBase
{
    private readonly DealMeDbContext _db;
    private readonly CompetitionEntryJob _job;
    private readonly ILogger<CompetitionsController> _logger;

    public CompetitionsController(DealMeDbContext db, CompetitionEntryJob job, ILogger<CompetitionsController> logger)
    {
        _db = db;
        _job = job;
        _logger = logger;
    }

    /// <summary>Competition opportunities that haven't been entry-attempted yet.</summary>
    [HttpGet("available")]
    public async Task<IActionResult> GetAvailable([FromQuery] int limit = 50, CancellationToken ct = default)
    {
        // EF can't combine our string-stored DateTimeOffset comparison with a NOT-IN
        // against an in-memory list in one translated query, so fetch type-filtered
        // rows and do attempt/expiry filtering client-side (comp volume is small).
        var attemptedIds = (await _db.CompetitionEntries.Select(e => e.OpportunityId).ToListAsync(ct))
            .ToHashSet();
        var now = DateTimeOffset.UtcNow;

        var rows = await _db.Opportunities
            .Where(o => o.Type == OpportunityType.Competition)
            .OrderByDescending(o => o.CreatedAt)
            .Take(limit * 4)
            .ToListAsync(ct);

        var list = rows
            .Where(o => !attemptedIds.Contains(o.Id))
            .Where(o => o.ExpiresAt == null || o.ExpiresAt > now)
            .Take(limit)
            .Select(o => new
            {
                o.Id,
                o.Title,
                o.Url,
                o.Description,
                o.Source,
                o.CreatedAt,
                o.ExpiresAt,
            });

        return Ok(list);
    }

    /// <summary>Competitions we've attempted, newest first.</summary>
    [HttpGet("entered")]
    public async Task<IActionResult> GetEntered([FromQuery] int limit = 100, CancellationToken ct = default)
    {
        var entries = await _db.CompetitionEntries
            .OrderByDescending(e => e.CreatedAt)
            .Take(limit)
            .Select(e => new
            {
                e.Id,
                e.OpportunityId,
                e.Title,
                e.CompetitionUrl,
                e.SourceUrl,
                e.Source,
                Status = e.Status.ToString(),
                e.Reason,
                e.EmailUsed,
                e.AttemptedAt,
                e.ExpiresAt,
                e.CreatedAt,
            })
            .ToListAsync(ct);

        return Ok(entries);
    }

    /// <summary>Run the auto-entry batch now (same code as the scheduled job).</summary>
    [HttpPost("run-batch")]
    public async Task<IActionResult> RunBatch()
    {
        _logger.LogInformation("Manual competition batch triggered");
        await _job.RunAsync(CancellationToken.None);
        return Ok(new { ok = true });
    }

    /// <summary>Summary counts for the UI badge.</summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var attempted = await _db.CompetitionEntries
            .Select(e => new { e.Status, e.OpportunityId, e.CreatedAt })
            .ToListAsync(ct);

        // Client-side expiry filter — see GetAvailable for why.
        var opps = await _db.Opportunities
            .Where(o => o.Type == OpportunityType.Competition)
            .Select(o => new { o.Id, o.ExpiresAt })
            .ToListAsync(ct);

        var attemptedIds = attempted.Select(a => a.OpportunityId).ToHashSet();
        var availableCount = opps
            .Count(o => !attemptedIds.Contains(o.Id) && (o.ExpiresAt == null || o.ExpiresAt > now));

        return Ok(new
        {
            available = availableCount,
            entered = attempted.Count(a => a.Status == EntryStatus.Entered),
            skipped = attempted.Count(a => a.Status == EntryStatus.Skipped),
            failed = attempted.Count(a => a.Status == EntryStatus.Failed),
        });
    }
}
