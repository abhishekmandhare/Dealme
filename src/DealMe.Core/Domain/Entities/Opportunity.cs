namespace DealMe.Core.Domain.Entities;

using DealMe.Core.Domain.Enums;

public class Opportunity
{
    public Guid Id { get; set; }
    public string Source { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public OpportunityType Type { get; set; }
    public OpportunityStatus Status { get; set; } = OpportunityStatus.New;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal? Value { get; set; }
    public decimal? OriginalValue { get; set; }
    public int? DiscountPercent { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public ConfidenceLevel Confidence { get; set; }
    public string Tags { get; set; } = "[]";
    public string Metadata { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
