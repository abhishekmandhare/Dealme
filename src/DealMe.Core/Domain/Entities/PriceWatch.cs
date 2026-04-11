namespace DealMe.Core.Domain.Entities;

public class PriceWatch
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SearchQuery { get; set; } = string.Empty;
    public string? CheapestUrl { get; set; }          // best URL found at last poll
    public string? CheapestRetailer { get; set; }     // retailer name for CheapestUrl
    public decimal TargetPrice { get; set; }
    public decimal? CurrentPrice { get; set; }        // cheapest price found at last poll
    public bool IsActive { get; set; } = true;
    // CdioWatchId kept for future use — not populated while CDIO integration is disabled
    public string? CdioWatchId { get; set; }
    public DateTimeOffset? LastCheckedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
