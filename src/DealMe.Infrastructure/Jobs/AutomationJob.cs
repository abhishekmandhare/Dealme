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

    public async Task RunBingSearchesAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("AutomationJob: starting Bing searches");
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
                Task = "bing-searches",
                Success = success,
                ItemsCompleted = searches,
                ItemsTotal = 33,
                PointsAfter = pointsAfter,
                LogOutput = logLines,
                StartedAt = startedAt,
                CompletedAt = DateTimeOffset.UtcNow,
            });
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("AutomationJob: Bing searches completed ({Searches}/33, success={Success})", searches, success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AutomationJob: Bing searches failed");

            _db.AutomationRuns.Add(new AutomationRun
            {
                Id = Guid.NewGuid(),
                Provider = "microsoft-rewards",
                Task = "bing-searches",
                Success = false,
                ItemsCompleted = 0,
                ItemsTotal = 33,
                Error = ex.Message,
                StartedAt = startedAt,
                CompletedAt = DateTimeOffset.UtcNow,
            });
            await _db.SaveChangesAsync(ct);
        }
    }
}
