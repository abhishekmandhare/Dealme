namespace DealMe.Api.Controllers;

using System.Text.Json;
using DealMe.Core.Domain.Entities;
using DealMe.Infrastructure.Jobs;
using DealMe.Infrastructure.Persistence;
using DealMe.Infrastructure.Services;
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

        var gorLastRun = lastRuns.FirstOrDefault(r => r.Provider == "google-opinion-rewards");

        var providers = new object[]
        {
            new
            {
                id = "google-opinion-rewards",
                name = "Google Opinion Rewards",
                description = "Completes Google Opinion Rewards surveys automatically via an Android emulator, earning Google Play credit.",
                tasks = new object[]
                {
                    new
                    {
                        id = "check-surveys",
                        name = "Check & Complete Surveys",
                        description = "Launches Google Opinion Rewards in the emulator, answers any available survey using Ollama (qwen2.5-coder:7b), and records the credit earned.",
                        schedule = "Every 6 hours",
                        maxPointsPerDay = 0,
                    },
                },
                loginRequired = true,
                loginInstructions = "Open http://<truenas-ip>:6080 (noVNC), install OpenGApps, sign into your Google account, then install Google Opinion Rewards from the Play Store.",
                serviceHealthy,
                lastRun = gorLastRun,
            },
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
                        schedule = AutomationMath.CronToAest(msConfig.CronSchedule),
                        maxPointsPerDay = 150,
                    },
                    new
                    {
                        id = "bing-searches-mobile",
                        name = "Bing Searches (Mobile)",
                        description = $"Performs up to {msConfig.MobileSearchCount} mobile searches using an Edge iOS user agent to earn ~100 points per day.",
                        schedule = AutomationMath.CronToAest(AutomationMath.OffsetCron(msConfig.CronSchedule, minutesOffset: 30)),
                        maxPointsPerDay = 100,
                    },
                    new
                    {
                        id = "daily-activities",
                        name = "Daily Activities",
                        description = "Clicks through the Daily Set and More Activities tiles on the rewards dashboard (quizzes, polls, article-reads) to earn ~60–100 points per day.",
                        schedule = AutomationMath.CronToAest(AutomationMath.OffsetCron(msConfig.CronSchedule, minutesOffset: 60)),
                        maxPointsPerDay = 80,
                    },
                    new
                    {
                        id = "redeem-amazon-5",
                        name = "Redeem $5 Amazon Gift Card",
                        description = "Spends 6,500 points to redeem a $5 Amazon AU gift card. Safely refuses to proceed if the balance is below the threshold.",
                        schedule = "Manual only",
                        maxPointsPerDay = 0,
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
                    AutomationMath.OffsetCron(body.CronSchedule, minutesOffset: 30));
                _jobs.AddOrUpdate<AutomationJob>(
                    "daily-activities",
                    job => job.RunDailyActivitiesAsync(CancellationToken.None),
                    AutomationMath.OffsetCron(body.CronSchedule, minutesOffset: 60));
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

        var allowed = new Dictionary<string, string[]>
        {
            ["microsoft-rewards"]      = ["bing-searches", "bing-searches-mobile", "daily-activities", "redeem-amazon-5"],
            ["google-opinion-rewards"] = ["check-surveys"],
        };
        if (!allowed.TryGetValue(provider, out var allowedTasks) || !allowedTasks.Contains(task))
            return BadRequest("Unknown provider/task");

        // Google Opinion Rewards — delegate directly to the automation service
        if (provider == "google-opinion-rewards")
        {
            var gorStart = DateTimeOffset.UtcNow;
            try
            {
                var resp = await _http.PostAsync($"{_automationUrl}/run/google-surveys",
                    new StringContent("{}", System.Text.Encoding.UTF8, "application/json"), ct);
                var json = await resp.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var success   = root.TryGetProperty("success",   out var s) && s.GetBoolean();
                var completed = root.TryGetProperty("completed", out var c) ? c.GetInt32() : 0;
                var logLines  = root.TryGetProperty("log",       out var lg) ? lg.ToString() : null;
                var error     = !success && root.TryGetProperty("error", out var err) ? err.GetString() : null;
                int? credBefore = root.TryGetProperty("creditsBefore", out var cb) && cb.ValueKind == JsonValueKind.Number ? cb.GetInt32() : null;
                int? credAfter  = root.TryGetProperty("creditsAfter",  out var ca) && ca.ValueKind == JsonValueKind.Number ? ca.GetInt32() : null;

                var run = new AutomationRun
                {
                    Id = Guid.NewGuid(), Provider = provider, Task = task,
                    Success = success, ItemsCompleted = completed, ItemsTotal = completed,
                    PointsBefore = credBefore, PointsAfter = credAfter,
                    Error = error, LogOutput = logLines,
                    StartedAt = gorStart, CompletedAt = DateTimeOffset.UtcNow,
                };
                _db.AutomationRuns.Add(run);
                await _db.SaveChangesAsync(ct);
                return Ok(run);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Automation {Provider}/{Task} failed", provider, task);
                var run = new AutomationRun
                {
                    Id = Guid.NewGuid(), Provider = provider, Task = task,
                    Success = false, ItemsCompleted = 0, ItemsTotal = 0,
                    Error = ex.Message, StartedAt = gorStart, CompletedAt = DateTimeOffset.UtcNow,
                };
                _db.AutomationRuns.Add(run);
                await _db.SaveChangesAsync(ct);
                return StatusCode(502, new { error = ex.Message });
            }
        }

        var config = await _db.AutomationProviderConfigs.FindAsync([provider], ct)
            ?? new AutomationProviderConfig { ProviderId = provider };

        var isMobile = task == "bing-searches-mobile";
        var isDailyActivities = task == "daily-activities";
        var isRedemption = task.StartsWith("redeem-");
        var itemsTotal = isRedemption ? 1 : (isDailyActivities ? 0 : (isMobile ? config.MobileSearchCount : config.SearchCount));
        var automationEndpoint = task switch
        {
            "bing-searches-mobile" => "/run/bing-searches-mobile",
            "daily-activities" => "/run/daily-activities",
            "redeem-amazon-5" => "/run/redeem",
            _ => "/run/bing-searches",
        };

        var startedAt = DateTimeOffset.UtcNow;

        AutomationRun NewRun(bool success, int completed, int total, int? pointsBefore, int? pointsAfter, string? error, string? log)
            => new()
            {
                Id = Guid.NewGuid(),
                Provider = provider,
                Task = task,
                Success = success,
                ItemsCompleted = completed,
                ItemsTotal = total,
                PointsBefore = pointsBefore,
                PointsAfter = pointsAfter,
                Error = error,
                LogOutput = log,
                StartedAt = startedAt,
                CompletedAt = DateTimeOffset.UtcNow,
            };

        try
        {
            var payload = isRedemption
                // Currently only one SKU is wired. When more are added, map task → (brand, denom).
                ? JsonSerializer.Serialize(new { brand = "amazon", denomination = 5, dryRun = false })
                : isDailyActivities
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
            // Searches return "searches"; daily-activities returns "completed"; redemption returns 1 on success.
            var completedCount = isRedemption
                ? (success ? 1 : 0)
                : isDailyActivities
                    ? (root.TryGetProperty("completed", out var cc) ? cc.GetInt32() : 0)
                    : (root.TryGetProperty("searches", out var sc) ? sc.GetInt32() : 0);
            var logLines = root.TryGetProperty("log", out var lg) ? lg.ToString() : null;
            var error = !success && root.TryGetProperty("error", out var err) ? err.GetString() : null;
            int? pointsBefore = root.TryGetProperty("pointsBefore", out var pb) && pb.ValueKind == JsonValueKind.Number
                ? pb.GetInt32() : null;
            int? pointsAfter = root.TryGetProperty("pointsAfter", out var pa) && pa.ValueKind == JsonValueKind.Number
                ? pa.GetInt32() : null;

            // Older automation responses only included the balance in the log text.
            if (pointsAfter == null && logLines != null)
            {
                var match = System.Text.RegularExpressions.Regex.Match(logLines, @"Points balance:\s*([\d,]+)");
                if (match.Success)
                    pointsAfter = int.Parse(match.Groups[1].Value.Replace(",", ""));
            }

            var run = NewRun(success, completedCount, isDailyActivities ? completedCount : itemsTotal,
                pointsBefore, pointsAfter, error, logLines);
            _db.AutomationRuns.Add(run);
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Automation {Provider}/{Task}: {Completed} items, success={Success}",
                provider, task, completedCount, success);

            return Ok(run);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Automation {Provider}/{Task} failed", provider, task);
            var run = NewRun(false, 0, itemsTotal, null, null, ex.Message, null);
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

        // Load all runs for this provider — we need data from before the
        // month window to establish the baseline balance (delta-based math).
        var allRuns = await _db.AutomationRuns
            .Where(r => r.Provider == provider)
            .OrderBy(r => r.StartedAt)
            .ToListAsync(ct);

        var runs = allRuns.Where(r => r.StartedAt >= monthAgo).ToList();
        var todayRuns = runs.Where(r => r.StartedAt.Date == today).ToList();
        var weekRuns = runs.Where(r => r.StartedAt >= weekAgo).ToList();

        var latestPoints = allRuns.LastOrDefault(r => r.PointsAfter.HasValue)?.PointsAfter;

        return Ok(new
        {
            currentPoints = latestPoints,
            today = new { runs = todayRuns.Count, pointsEarned = AutomationMath.EarnedInWindow(allRuns, today), success = todayRuns.Count(r => r.Success) },
            week = new { runs = weekRuns.Count, pointsEarned = AutomationMath.EarnedInWindow(allRuns, weekAgo), success = weekRuns.Count(r => r.Success) },
            month = new { runs = runs.Count, pointsEarned = AutomationMath.EarnedInWindow(allRuns, monthAgo), success = runs.Count(r => r.Success) },
            totalRuns = runs.Count,
        });
    }

}
