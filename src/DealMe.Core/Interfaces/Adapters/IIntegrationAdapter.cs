namespace DealMe.Core.Interfaces.Adapters;

using DealMe.Core.DTOs;

public interface IIntegrationAdapter
{
    /// <summary>Unique stable name: "ozbargain", "cheapies", "changedetection".</summary>
    string AdapterName { get; }

    /// <summary>Fetch raw data from the source. Returns raw payload (RSS XML, webhook body, etc.).</summary>
    Task<AdapterResult> FetchAsync(CancellationToken ct = default);

    /// <summary>Normalise raw payload into a list of Opportunity candidates.</summary>
    Task<IReadOnlyList<NormalisedOpportunity>> NormaliseAsync(AdapterResult raw, CancellationToken ct = default);

    /// <summary>Connectivity check. Returns true if the source is reachable.</summary>
    Task<bool> HealthCheckAsync(CancellationToken ct = default);

    /// <summary>Runtime status: last run time, error state, item count, etc.</summary>
    Task<AdapterStatus> GetStatusAsync(CancellationToken ct = default);
}
