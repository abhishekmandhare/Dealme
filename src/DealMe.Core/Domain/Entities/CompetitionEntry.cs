namespace DealMe.Core.Domain.Entities;

/// <summary>
/// Record of our auto-entry attempt against a competition opportunity.
/// One row per (opportunity) — the EntryStatus captures what happened.
/// </summary>
public class CompetitionEntry
{
    public Guid Id { get; set; }

    /// <summary>FK to the Opportunity that represents the competition listing.</summary>
    public Guid OpportunityId { get; set; }

    /// <summary>Title at time of entry — denormalised so the Entered list doesn't need a join.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>The external URL the user would visit (the competition host, not OzBargain).</summary>
    public string CompetitionUrl { get; set; } = string.Empty;

    /// <summary>OzBargain (or other source) deal page URL.</summary>
    public string SourceUrl { get; set; } = string.Empty;

    /// <summary>Source adapter name (e.g. "ozbargain-competitions").</summary>
    public string Source { get; set; } = string.Empty;

    public EntryStatus Status { get; set; } = EntryStatus.Pending;

    /// <summary>
    /// Why we skipped / failed. Examples: "captcha_detected", "no_email_field",
    /// "complex_form", "submission_error".
    /// </summary>
    public string? Reason { get; set; }

    public string? EmailUsed { get; set; }

    public DateTimeOffset? AttemptedAt { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

public enum EntryStatus : byte
{
    Pending = 0,      // queued, not yet attempted
    Entered = 1,      // form submitted, no error detected
    Skipped = 2,      // we saw the page and chose not to attempt (captcha, complex form, etc.)
    Failed = 3,       // we attempted but submission errored
}
