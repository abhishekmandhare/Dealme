namespace DealMe.Infrastructure.Services;

using DealMe.Core.Domain.Entities;

/// <summary>
/// Pure helpers for automation scheduling + stats. Extracted so they're
/// unit-testable without needing an API/DB fixture.
/// </summary>
public static class AutomationMath
{
    /// <summary>
    /// Shift a simple "M H * * *" daily cron by N minutes. Falls back to the input
    /// if the expression isn't the expected daily shape.
    /// </summary>
    public static string OffsetCron(string cron, int minutesOffset)
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

    /// <summary>
    /// Convert a simple "M H * * *" daily cron (in UTC) to a human-readable AEST label.
    /// AEST is UTC+10 (we don't currently model DST → AEDT).
    /// </summary>
    public static string CronToAest(string cron)
    {
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

    /// <summary>
    /// Points earned within [windowStart, now], computed as the delta between the
    /// latest balance snapshot before the window (or the earliest snapshot inside
    /// the window if there's no prior data) and the latest snapshot overall.
    /// Returns null when there's no balance data at all. Negative deltas (e.g. after
    /// a redemption) are clamped to 0 to avoid confusing "earned" totals.
    /// </summary>
    public static int? EarnedInWindow(IReadOnlyList<AutomationRun> runs, DateTimeOffset windowStart)
    {
        var withPoints = runs.Where(r => r.PointsAfter.HasValue).ToList();
        if (withPoints.Count == 0) return null;

        var baseline = withPoints.LastOrDefault(r => r.StartedAt < windowStart)?.PointsAfter
                       ?? withPoints.FirstOrDefault(r => r.StartedAt >= windowStart)?.PointsAfter;
        var current = withPoints[^1].PointsAfter;

        if (baseline is null || current is null) return null;
        var delta = current.Value - baseline.Value;
        return delta < 0 ? 0 : delta;
    }
}
