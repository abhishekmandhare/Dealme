namespace DealMe.Api.Controllers;

using System.Text.Json;
using DealMe.Core.Domain.Entities;
using DealMe.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

[ApiController]
[Route("api/[controller]")]
public class AutomationController : ControllerBase
{
    private readonly DealMeDbContext _db;
    private readonly HttpClient _http;
    private readonly string _automationUrl;
    private readonly ILogger<AutomationController> _logger;

    public AutomationController(
        DealMeDbContext db,
        IHttpClientFactory httpFactory,
        IConfiguration config,
        ILogger<AutomationController> logger)
    {
        _db = db;
        _http = httpFactory.CreateClient("automation");
        _automationUrl = (config["AUTOMATION_URL"] ?? "http://automation:3100").TrimEnd('/');
        _logger = logger;
    }

    /// <summary>All known automation providers and their current status.</summary>
    [HttpGet("providers")]
    public async Task<IActionResult> GetProviders(CancellationToken ct)
    {
        var providers = new[]
        {
            new
            {
                id = "microsoft-rewards",
                name = "Microsoft Rewards",
                description = "Earn points by performing Bing searches. Points can be redeemed for gift cards, sweepstakes entries, and donations.",
                tasks = new[]
                {
                    new
                    {
                        id = "bing-searches",
                        name = "Bing Searches",
                        description = "Performs up to 33 desktop searches on Bing to earn ~150 points per day. Searches use randomised queries with natural delays between them.",
                        schedule = "Daily at 7:00 AM AEST",
                        maxPointsPerDay = 150,
                    },
                },
                loginRequired = true,
                loginInstructions = "Run the login script on the server to authenticate your Microsoft account. The session is stored in a persistent browser profile.",
            },
        };

        // Get last run for each provider
        var lastRuns = await _db.AutomationRuns
            .GroupBy(r => r.Provider)
            .Select(g => g.OrderByDescending(r => r.StartedAt).First())
            .ToListAsync(ct);

        // Check automation service health
        bool serviceHealthy;
        try
        {
            var resp = await _http.GetAsync($"{_automationUrl}/health", ct);
            serviceHealthy = resp.IsSuccessStatusCode;
        }
        catch
        {
            serviceHealthy = false;
        }

        var result = providers.Select(p => new
        {
            p.id,
            p.name,
            p.description,
            p.tasks,
            p.loginRequired,
            p.loginInstructions,
            serviceHealthy,
            lastRun = lastRuns.FirstOrDefault(r => r.Provider == p.id),
        });

        return Ok(result);
    }

    /// <summary>Run history for a specific provider.</summary>
    [HttpGet("providers/{provider}/history")]
    public async Task<IActionResult> GetHistory(string provider, [FromQuery] int limit = 30, CancellationToken ct = default)
    {
        var runs = await _db.AutomationRuns
            .Where(r => r.Provider == provider)
            .OrderByDescending(r => r.StartedAt)
            .Take(limit)
            .Select(r => new
            {
                r.Id,
                r.Provider,
                r.Task,
                r.Success,
                r.ItemsCompleted,
                r.ItemsTotal,
                r.PointsBefore,
                r.PointsAfter,
                r.Error,
                r.StartedAt,
                r.CompletedAt,
            })
            .ToListAsync(ct);

        return Ok(runs);
    }

    /// <summary>Trigger a task run for a provider.</summary>
    [HttpPost("providers/{provider}/run/{task}")]
    public async Task<IActionResult> RunTask(string provider, string task, CancellationToken ct)
    {
        if (provider != "microsoft-rewards" || task != "bing-searches")
            return BadRequest("Unknown provider/task");

        var startedAt = DateTimeOffset.UtcNow;

        try
        {
            var resp = await _http.PostAsync($"{_automationUrl}/run/bing-searches", null, ct);
            var json = await resp.Content.ReadAsStringAsync(ct);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var success = root.TryGetProperty("success", out var s) && s.GetBoolean();
            var searches = root.TryGetProperty("searches", out var sc) ? sc.GetInt32() : 0;
            var logLines = root.TryGetProperty("log", out var lg) ? lg.ToString() : null;
            var error = !success && root.TryGetProperty("error", out var err) ? err.GetString() : null;

            // Try to extract points from log
            int? pointsAfter = null;
            if (logLines != null)
            {
                var pointsMatch = System.Text.RegularExpressions.Regex.Match(
                    logLines, @"Points balance:\s*([\d,]+)");
                if (pointsMatch.Success)
                    pointsAfter = int.Parse(pointsMatch.Groups[1].Value.Replace(",", ""));
            }

            var run = new AutomationRun
            {
                Id = Guid.NewGuid(),
                Provider = provider,
                Task = task,
                Success = success,
                ItemsCompleted = searches,
                ItemsTotal = 33,
                PointsAfter = pointsAfter,
                Error = error,
                LogOutput = logLines,
                StartedAt = startedAt,
                CompletedAt = DateTimeOffset.UtcNow,
            };
            _db.AutomationRuns.Add(run);
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Automation {Provider}/{Task}: {Searches} searches, success={Success}",
                provider, task, searches, success);

            return Ok(run);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Automation {Provider}/{Task} failed", provider, task);

            var run = new AutomationRun
            {
                Id = Guid.NewGuid(),
                Provider = provider,
                Task = task,
                Success = false,
                ItemsCompleted = 0,
                ItemsTotal = 33,
                Error = ex.Message,
                StartedAt = startedAt,
                CompletedAt = DateTimeOffset.UtcNow,
            };
            _db.AutomationRuns.Add(run);
            await _db.SaveChangesAsync(ct);

            return StatusCode(502, new { error = ex.Message });
        }
    }

    /// <summary>Stats summary for a provider.</summary>
    [HttpGet("providers/{provider}/stats")]
    public async Task<IActionResult> GetStats(string provider, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var today = now.Date;
        var weekAgo = now.AddDays(-7);
        var monthAgo = now.AddDays(-30);

        var runs = await _db.AutomationRuns
            .Where(r => r.Provider == provider && r.StartedAt >= monthAgo)
            .OrderByDescending(r => r.StartedAt)
            .ToListAsync(ct);

        var todayRuns = runs.Where(r => r.StartedAt.Date == today).ToList();
        var weekRuns = runs.Where(r => r.StartedAt >= weekAgo).ToList();

        // Estimate points earned from successful runs
        var todayPoints = todayRuns.Where(r => r.Success).Sum(r => r.ItemsCompleted * 5);
        var weekPoints = weekRuns.Where(r => r.Success).Sum(r => r.ItemsCompleted * 5);
        var monthPoints = runs.Where(r => r.Success).Sum(r => r.ItemsCompleted * 5);
        var latestPoints = runs.FirstOrDefault(r => r.PointsAfter.HasValue)?.PointsAfter;

        return Ok(new
        {
            currentPoints = latestPoints,
            today = new { runs = todayRuns.Count, pointsEstimate = todayPoints, success = todayRuns.Count(r => r.Success) },
            week = new { runs = weekRuns.Count, pointsEstimate = weekPoints, success = weekRuns.Count(r => r.Success) },
            month = new { runs = runs.Count, pointsEstimate = monthPoints, success = runs.Count(r => r.Success) },
            totalRuns = runs.Count,
        });
    }
}
