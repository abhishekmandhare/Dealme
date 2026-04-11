namespace DealMe.Core.DTOs;

public sealed record AdapterResult(
    string AdapterName,
    bool Success,
    string? RawPayload,
    string? ErrorMessage,
    DateTimeOffset FetchedAt);
