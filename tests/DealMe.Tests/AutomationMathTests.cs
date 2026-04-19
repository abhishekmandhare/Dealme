namespace DealMe.Tests;

using DealMe.Core.Domain.Entities;
using DealMe.Infrastructure.Services;
using Xunit;

public class AutomationMathTests
{
    // ── OffsetCron ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("0 21 * * *", 30, "30 21 * * *")]
    [InlineData("0 21 * * *", 60, "0 22 * * *")]
    [InlineData("30 23 * * *", 60, "30 0 * * *")]      // wraps past midnight
    [InlineData("45 23 * * *", 20, "5 0 * * *")]       // minute wraps + hour carries
    public void OffsetCron_shifts_simple_daily_cron(string input, int offset, string expected)
    {
        Assert.Equal(expected, AutomationMath.OffsetCron(input, offset));
    }

    [Fact]
    public void OffsetCron_returns_input_unchanged_when_not_parseable()
    {
        // Non-numeric hour → we can't offset, so leave untouched.
        Assert.Equal("*/5 * * * *", AutomationMath.OffsetCron("*/5 * * * *", 30));
    }

    [Fact]
    public void OffsetCron_handles_negative_offset()
    {
        Assert.Equal("30 20 * * *", AutomationMath.OffsetCron("0 21 * * *", -30));
    }

    // ── CronToAest ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("0 21 * * *", "Daily at 7:00 AM AEST")]       // 21:00 UTC = 07:00 AEST
    [InlineData("30 21 * * *", "Daily at 7:30 AM AEST")]
    [InlineData("0 22 * * *", "Daily at 8:00 AM AEST")]
    [InlineData("0 2 * * *", "Daily at 12:00 PM AEST")]       // noon edge
    [InlineData("0 14 * * *", "Daily at 12:00 AM AEST")]      // midnight edge
    public void CronToAest_formats_daily_cron_as_aest(string cron, string expected)
    {
        Assert.Equal(expected, AutomationMath.CronToAest(cron));
    }

    [Fact]
    public void CronToAest_falls_back_for_non_daily_cron()
    {
        Assert.Equal("Cron: */5 * * * *", AutomationMath.CronToAest("*/5 * * * *"));
    }

    // ── EarnedInWindow ────────────────────────────────────────────────────

    private static AutomationRun Run(DateTimeOffset at, int? pointsAfter) =>
        new()
        {
            Id = Guid.NewGuid(),
            Provider = "p",
            Task = "t",
            Success = true,
            StartedAt = at,
            CompletedAt = at.AddMinutes(1),
            PointsAfter = pointsAfter,
        };

    [Fact]
    public void EarnedInWindow_returns_null_when_no_snapshots()
    {
        var runs = new List<AutomationRun>
        {
            Run(DateTimeOffset.UtcNow.AddHours(-1), null),
        };
        Assert.Null(AutomationMath.EarnedInWindow(runs, DateTimeOffset.UtcNow.Date));
    }

    [Fact]
    public void EarnedInWindow_uses_last_snapshot_before_window_as_baseline()
    {
        var today = DateTimeOffset.UtcNow.Date;
        var runs = new List<AutomationRun>
        {
            Run(today.AddDays(-1), 1000),  // yesterday's closing balance
            Run(today.AddHours(6), 1030),
            Run(today.AddHours(12), 1050),
        };
        // Earned today = 1050 - 1000 = 50
        Assert.Equal(50, AutomationMath.EarnedInWindow(runs, today));
    }

    [Fact]
    public void EarnedInWindow_falls_back_to_earliest_in_window_when_no_prior()
    {
        var today = DateTimeOffset.UtcNow.Date;
        var runs = new List<AutomationRun>
        {
            Run(today.AddHours(6), 1000),  // first snapshot of the day
            Run(today.AddHours(12), 1050),
        };
        Assert.Equal(50, AutomationMath.EarnedInWindow(runs, today));
    }

    [Fact]
    public void EarnedInWindow_clamps_negative_deltas_to_zero()
    {
        // Redemption scenario: balance dropped from 6,500 to 0.
        var today = DateTimeOffset.UtcNow.Date;
        var runs = new List<AutomationRun>
        {
            Run(today.AddDays(-1), 6500),
            Run(today.AddHours(6), 0),
        };
        Assert.Equal(0, AutomationMath.EarnedInWindow(runs, today));
    }

    [Fact]
    public void EarnedInWindow_returns_zero_when_single_snapshot()
    {
        // One snapshot → baseline == current → delta = 0.
        var today = DateTimeOffset.UtcNow.Date;
        var runs = new List<AutomationRun>
        {
            Run(today.AddHours(6), 1794),
        };
        Assert.Equal(0, AutomationMath.EarnedInWindow(runs, today));
    }
}
