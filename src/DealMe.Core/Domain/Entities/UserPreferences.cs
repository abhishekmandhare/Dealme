namespace DealMe.Core.Domain.Entities;

public class UserPreferences
{
    public Guid Id { get; set; }
    public decimal MinDealValue { get; set; } = 5.00m;
    public TimeOnly? QuietHoursStart { get; set; }
    public TimeOnly? QuietHoursEnd { get; set; }
    public string Timezone { get; set; } = "Australia/Melbourne";
    public string EnabledCategories { get; set; } = "[]";
    public string InterestTags { get; set; } = "[]";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
