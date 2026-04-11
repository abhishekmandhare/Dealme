namespace DealMe.Core.DTOs;

public sealed record AdapterStatus(
    string AdapterName,
    bool IsHealthy,
    DateTimeOffset? LastRunAt,
    int ItemsLastRun,
    string? LastError);
