namespace DealMe.Api.Controllers;

using System.Text.Json;
using DealMe.Core.Domain.Entities;
using DealMe.Infrastructure.Jobs;
using DealMe.Infrastructure.Persistence;
using Hangfire;
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
    private readonly IRecurringJobManager _jobs;
    private readonly ILogger<AutomationController> _logger;

    public AutomationController(
        DealMeDbContext db,
        IHttpClientFactory httpFactory,
        IConfiguration config,
        IRecurringJobManager jobs,
        ILogger<AutomationController> logger)
    {
        _db = db;
        _http = httpFactory.CreateClient("automation");
        _automationUrl = (config["AUTOMATION_URL"] ?? "http://automation:3100").TrimEnd('/');
        _jobs = jobs;
        _logger = logger;
    }

    /// <summary>All known automation providers and their current status.</summary>
    [HttpGet("providers")]
    public async Task<IActionResult> GetProviders(CancellationToken ct)
    {
        var configs = await _db.AutomationProviderConfigs.ToListAsync(ct);
        var lastRuns = await _db.AutomationRuns
            .GroupBy(r => r.Provider)
            .Select(g => g.OrderByDescending(r => r.StartedAt).First())
            .ToListAsync(ct);

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

        var msConfig = configs.FirstOrDefault(c => c.ProviderId == "microsoft-rewards")
            ?? new AutomationProviderConfig { ProviderId = "microsoft-rewards" };

        var providers = new[]
        {
            new
            {
                id = "microsoft-rewards",
                name = "Microsoft Rewards",
                description = "Earn points by performing Bing searches. Points can be redeemed for gift cards, sweepstakes entries, and donations.",
                tasks = new object[]
                {
                    new
                    {
                        id = "bing-searches",
                        name = "Bing Searches (Desktop)",
                        description = $"Performs up to {msConfig.SearchCount} desktop searches on Bing to earn ~150 points per day.",
                        schedule = CronToAest(msConfig.CronSchedule),
                        maxPointsPerDay = 150,
                    },
                    new
                    {
                        id = "bing-searches-mobile",
                        name = "Bing Searches (Mobile)",
                        description = $"Performs up to {msConfig.MobileSearchCount} mobile searches using an Edge iOS user agent to earn ~100 points per day.",
                        schedule = CronToAest(OffsetCron(msConfig.CronSchedule, minutesOffset: 30)),
                        maxPointsPerDay = 100,
                    },
                    new
                    {
                        id = "daily-activities",
                        name = "Daily Activities",
                        description = "Clicks through the Daily Set and More Activities tiles on the rewards dashboard (quizzes, polls, article-reads) to earn ~60–100 points per day.",
                        schedule = CronToAest(OffsetCron(msConfig.CronSchedule, minutesOffset: 60)),
                        maxPointsPerDay = 80,
                    },
                },
                loginRequired = true,
                loginInstructions = "Run the login script on the server to authenticate your Microsoft account. The session is stored in a persistent browser profile.",
                serviceHealthy,
                lastRun = lastRuns.FirstOrDefault(r => r.Provider == "microsoft-rewards"),
            },
        };

        return Ok(providers);
    }

    /// <summary>Config for a specific provider.</summary>
    [HttpGet("providers/{provider}/config")]
    public async Task<IActionResult> GetConfig(string provider, CancellationToken ct)
    {
        var config = await _db.AutomationProviderConfigs.FindAsync([provider], ct)
            ?? new AutomationProviderConfig { ProviderId = provider };
        return Ok(config);
    }

    /// <summary>Update config for a specific provider.</summary>
    [HttpPut("providers/{provider}/config")]
    public async Task<IActionResult> PutConfig(string provider, [FromBody] AutomationProviderConfig body, CancellationToken ct)
    {
        if (provider != body.ProviderId)
            return BadRequest("Provider ID mismatch");

        var existing = await _db.AutomationProviderConfigs.FindAsync([provider], ct);
        if (existing == null)
        {
            _db.AutomationProviderConfigs.Add(body);
        }
        else
        {
            existing.AccountLabel = body.AccountLabel;
            existing.CronSchedule = body.CronSchedule;
            existing.SearchCount = body.SearchCount;
            existing.MobileSearchCount = body.MobileSearchCount;
            existing.MinDelayMs = body.MinDelayMs;
            existing.MaxDelayMs = body.MaxDelayMs;
            existing.Enabled = body.Enabled;
        }

        await _db.SaveChangesAsync(ct);

        // Update Hangfire schedule if microsoft-rewards
        if (provider == "microsoft-rewards")
        {
            if (body.Enabled)
            {
                _jobs.AddOrUpdate<AutomationJob>(
                    "bing-searches",
                    job => job.RunBingSearchesAsync(CancellationToken.None),
                    body.CronSchedule);
                _jobs.AddOrUpdate<AutomationJob>(
                    "bing-searches-mobile",
                    job => job.RunBingMobileSearchesAsync(CancellationToken.None),
                    OffsetCron(body.CronSchedule, minutesOffset: 30));
                _jobs.AddOrUpdate<AutomationJob>(
                    "daily-activities",
                    job => job.RunDailyActivitiesAsync(CancellationToken.None),
                    OffsetCron(body.CronSchedule, minutesOffset: 60));
            }
            else
            {
                _jobs.RemoveIfExists("bing-searches");
                _jobs.RemoveIfExists("bing-searches-mobile");
                _jobs.RemoveIfExists("daily-activities");
            }
        }

        _logger.LogInformation("Automation config updated for {Provider}", provider);
        return Ok(body);
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
    public async Task<IActionResult> RunTask(string provider, string task)
    {
        // Intentionally ignore the request cancellation token: the downstream automation
        // call takes minutes to complete, and we don't want a browser tab being closed
        // (or a proxy timeout) to kill the run mid-flight and leave the DB without a record.
        var ct = CancellationToken.None;

        if (provider != "microsoft-rewards"
            || (task != "bing-searches" && task != "bing-searches-mobile" && task != "daily-activities"))
            return BadRequest("Unknown provider/task");

        var config = await _db.AutomationProviderConfigs.FindAsync([provider], ct)
            ?? new AutomationProviderConfig { ProviderId = provider };

        var isMobile = task == "bing-searches-mobile";
        var isDailyActivities = task == "daily-activities";
        var itemsTotal = isDailyActivities ? 0 : (isMobile ? config.MobileSearchCount : config.SearchCount);
        var automationEndpoint = task switch
        {
            "bing-searches-mobile" => "/run/bing-searches-mobile",
            "daily-activities" => "/run/daily-activities",
            _ => "/run/bing-searches",
        };

        var startedAt = DateTimeOffset.UtcNow;

        try
        {
            var payload = isDailyActivities
                ? "{}"
                : JsonSerializer.Serialize(new
                {
                    searchCount = itemsTotal,
                    minDelay = config.MinDelayMs,
                    maxDelay = config.MaxDelayMs,
                });
            var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
            var resp = await _http.PostAsync($"{_automationUrl}{automationEndpoint}", content, ct);
            var json = await resp.Content.ReadAsStringAsync(ct);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var success = root.TryGetProperty("success", out var s) && s.GetBoolean();
            // Searches return "searches"; daily-activities returns "completed".
            var completedCount = isDailyActivities
                ? (root.TryGetProperty("completed", out var cc) ? cc.GetInt32() : 0)
                : (root.TryGetProperty("searches", out var sc) ? sc.GetInt32() : 0);
            var logLines = root.TryGetProperty("log", out var lg) ? lg.ToString() : null;
            var error = !success && root.TryGetProperty("error", out var err) ? err.GetString() : null;

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
                ItemsCompleted = completedCount,
                ItemsTotal = isDailyActivities ? completedCount : itemsTotal,
                PointsAfter = pointsAfter,
                Error = error,
                LogOutput = logLines,
                StartedAt = startedAt,
                CompletedAt = DateTimeOffset.UtcNow,
            };
            _db.AutomationRuns.Add(run);
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Automation {Provider}/{Task}: {Completed} items, success={Success}",
                provider, task, completedCount, success);

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
                ItemsTotal = itemsTotal,
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

    /// <summary>Shift a simple "M H * * *" daily cron by N minutes. Falls back to input if unparseable.</summary>
    private static string OffsetCron(string cron, int minutesOffset)
    {
        var parts = cron.Split(' ');
        if (parts.Length == 5
            && int.TryParse(parts[0], out var min)
            && int.TryParse(parts[1], out var hour))
        {
            var total = (hour * 60 + min + minutesOffset) % (24 * 60);
            if (total < 0) total += 24 * 60;
            return $"{total % 60} {total / 60} {parts[2]} {parts[3]} {parts[4]}";
        }
        return cron;
    }

    private static string CronToAest(string cron)
    {
        // Convert simple "M H * * *" daily cron (UTC) to a human-readable AEST label
        var parts = cron.Split(' ');
        if (parts.Length == 5
            && int.TryParse(parts[0], out var min)
            && int.TryParse(parts[1], out var hourUtc)
            && parts[2] == "*" && parts[3] == "*" && parts[4] == "*")
        {
            var hourAest = (hourUtc + 10) % 24;
            var period = hourAest >= 12 ? "PM" : "AM";
            var displayHour = hourAest % 12 == 0 ? 12 : hourAest % 12;
            return $"Daily at {displayHour}:{min:D2} {period} AEST";
        }
        return $"Cron: {cron}";
    }
}
