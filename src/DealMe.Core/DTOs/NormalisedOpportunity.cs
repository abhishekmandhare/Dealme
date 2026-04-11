namespace DealMe.Core.DTOs;

using DealMe.Core.Domain.Enums;

public sealed record NormalisedOpportunity(
    string Source,
    string SourceId,
    OpportunityType Type,
    string Title,
    string? Description,
    decimal? Value,
    decimal? OriginalValue,
    int? DiscountPercent,
    string Url,
    string? ImageUrl,
    DateTimeOffset? ExpiresAt,
    ConfidenceLevel Confidence,
    string[] Tags,
    Dictionary<string, object?> Metadata);
