namespace DealMe.Core.Domain.Entities;

public class AutomationRun
{
    public Guid Id { get; set; }
    public string Provider { get; set; } = string.Empty;       // "microsoft-rewards"
    public string Task { get; set; } = string.Empty;           // "bing-searches"
    public bool Success { get; set; }
    public int ItemsCompleted { get; set; }                    // e.g. 33 searches
    public int ItemsTotal { get; set; }                        // e.g. 33 target
    public int? PointsBefore { get; set; }
    public int? PointsAfter { get; set; }
    public string? Error { get; set; }
    public string? LogOutput { get; set; }                     // JSON array of log lines
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset CompletedAt { get; set; }
}
