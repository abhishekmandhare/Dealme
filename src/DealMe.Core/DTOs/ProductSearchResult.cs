namespace DealMe.Core.DTOs;

public sealed record ProductSearchResult(
    string Name,
    string Retailer,
    decimal Price,
    string Url);
