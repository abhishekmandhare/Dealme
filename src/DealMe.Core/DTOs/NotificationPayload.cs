namespace DealMe.Core.DTOs;

public sealed record NotificationPayload(
    string Title,
    string Body,
    IReadOnlyList<string> AppriseUrls);
