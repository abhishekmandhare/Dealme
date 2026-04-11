namespace DealMe.Core.Interfaces.Pipeline;

using DealMe.Core.DTOs;

public interface IPipelineService
{
    /// <summary>Run the full Normalise → Dedup → Filter → Score → Notify pipeline for a batch of candidates.</summary>
    Task ProcessAsync(IReadOnlyList<NormalisedOpportunity> candidates, Guid correlationId, CancellationToken ct = default);
}
