namespace DealMe.Core.Domain.Entities;

public class AutomationProviderConfig
{
    public string ProviderId { get; set; } = string.Empty;
    public string? AccountLabel { get; set; }
    public string CronSchedule { get; set; } = "0 21 * * *";  // 7:00 AM AEST default
    public int SearchCount { get; set; } = 33;
    public int MobileSearchCount { get; set; } = 20;
    public int MinDelayMs { get; set; } = 8000;
    public int MaxDelayMs { get; set; } = 20000;
    public bool Enabled { get; set; } = true;
}
