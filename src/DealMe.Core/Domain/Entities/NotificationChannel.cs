namespace DealMe.Core.Domain.Entities;

public class NotificationChannel
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string AppriseUrl { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
