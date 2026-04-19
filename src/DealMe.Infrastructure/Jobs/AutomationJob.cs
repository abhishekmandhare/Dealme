namespace DealMe.Infrastructure.Jobs;

using System.Text.Json;
using DealMe.Core.Domain.Entities;
using DealMe.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

public sealed class AutomationJob
{
    private readonly DealMeDbContext _db;
    private readonly HttpClient _http;
    private readonly string _automationUrl;
    private readonly ILogger<AutomationJob> _logger;

    public AutomationJob(
        DealMeDbContext db,
        IHttpClientFactory httpFactory,
        IConfiguration config,
        ILogger<AutomationJob> logger)
    {
        _db = db;
        _http = httpFactory.CreateClient("automation");
        _automationUrl = (config["AUTOMATION_URL"] ?? "http://automation:3100").TrimEnd('/');
        _logger = logger;
    }

    public Task RunBingSearchesAsync(CancellationToken ct = default)
        => RunTaskAsync("bing-searches", "/run/bing-searches", useDesktop: true, ct);

    public Task RunBingMobileSearchesAsync(CancellationToken ct = default)
        => RunTaskAsync("bing-searches-mobile", "/run/bing-searches-mobile", useDesktop: false, ct);

    public async Task RunDailyActivitiesAsync(CancellationToken ct = default)
    {
        const string task = "daily-activities";
        _logger.LogInformation("AutomationJob: starting {Task}", task);
        var startedAt = DateTimeOffset.UtcNow;

        try
        {
            var resp = await _http.PostAsync(
                $"{_automationUrl}/run/daily-activities",
                new StringContent("{}", System.Text.Encoding.UTF8, "application/json"),
                ct);
            var json = await resp.Content.ReadAsStringAsync(ct);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var success = root.TryGetProperty("success", out var s) && s.GetBoolean();
            var processed = root.TryGetProperty("completed", out var c) ? c.GetInt32() : 0;
            var logLines = root.TryGetProperty("log", out var lg) ? lg.ToString() : null;

            int? pointsAfter = null;
            if (logLines != null)
            {
                var m = System.Text.RegularExpressions.Regex.Match(logLines, @"Points balance:\s*([\d,]+)");
                if (m.Success) pointsAfter = int.Parse(m.Groups[1].Value.Replace(",", ""));
            }

            _db.AutomationRuns.Add(new AutomationRun
            {
                Id = Guid.NewGuid(),
                Provider = "microsoft-rewards",
                Task = task,
                Success = success,
                ItemsCompleted = processed,
                ItemsTotal = processed,
                PointsAfter = pointsAfter,
                LogOutput = logLines,
                StartedAt = startedAt,
                CompletedAt = DateTimeOffset.UtcNow,
            });
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("AutomationJob: {Task} completed ({Processed} activities, success={Success})",
                task, processed, success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AutomationJob: {Task} failed", task);
            _db.AutomationRuns.Add(new AutomationRun
            {
                Id = Guid.NewGuid(),
                Provider = "microsoft-rewards",
                Task = task,
                Success = false,
                ItemsCompleted = 0,
                ItemsTotal = 0,
                Error = ex.Message,
                StartedAt = startedAt,
                CompletedAt = DateTimeOffset.UtcNow,
            });
            await _db.SaveChangesAsync(ct);
        }
    }

    private async Task RunTaskAsync(string task, string endpoint, bool useDesktop, CancellationToken ct)
    {
        _logger.LogInformation("AutomationJob: starting {Task}", task);
        var startedAt = DateTimeOffset.UtcNow;

        var config = await _db.AutomationProviderConfigs.FindAsync(["microsoft-rewards"], ct)
            ?? new AutomationProviderConfig { ProviderId = "microsoft-rewards" };

        var searchCount = useDesktop ? config.SearchCount : config.MobileSearchCount;

        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                searchCount,
                minDelay = config.MinDelayMs,
                maxDelay = config.MaxDelayMs,
            });
            var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
            var resp = await _http.PostAsync($"{_automationUrl}{endpoint}", content, ct);
            var json = await resp.Content.ReadAsStringAsync(ct);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var success = root.TryGetProperty("success", out var s) && s.GetBoolean();
            var searches = root.TryGetProperty("searches", out var sc) ? sc.GetInt32() : 0;
            var logLines = root.TryGetProperty("log", out var lg) ? lg.ToString() : null;

            int? pointsAfter = null;
            if (logLines != null)
            {
                var m = System.Text.RegularExpressions.Regex.Match(logLines, @"Points balance:\s*([\d,]+)");
                if (m.Success) pointsAfter = int.Parse(m.Groups[1].Value.Replace(",", ""));
            }

            _db.AutomationRuns.Add(new AutomationRun
            {
                Id = Guid.NewGuid(),
                Provider = "microsoft-rewards",
                Task = task,
                Success = success,
                ItemsCompleted = searches,
                ItemsTotal = searchCount,
                PointsAfter = pointsAfter,
                LogOutput = logLines,
                StartedAt = startedAt,
                CompletedAt = DateTimeOffset.UtcNow,
            });
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("AutomationJob: {Task} completed ({Searches} searches, success={Success})",
                task, searches, success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AutomationJob: {Task} failed", task);

            _db.AutomationRuns.Add(new AutomationRun
            {
                Id = Guid.NewGuid(),
                Provider = "microsoft-rewards",
                Task = task,
                Success = false,
                ItemsCompleted = 0,
                ItemsTotal = searchCount,
                Error = ex.Message,
                StartedAt = startedAt,
                CompletedAt = DateTimeOffset.UtcNow,
            });
            await _db.SaveChangesAsync(ct);
        }
    }
}
