namespace DealMe.Infrastructure.Jobs;

using DealMe.Core.Interfaces.Adapters;
using DealMe.Core.Interfaces.Pipeline;
using Microsoft.Extensions.Logging;

public sealed class AdapterPollingJob
{
    private readonly Dictionary<string, IIntegrationAdapter> _adapters;
    private readonly IPipelineService _pipeline;
    private readonly ILogger<AdapterPollingJob> _logger;

    public AdapterPollingJob(
        IEnumerable<IIntegrationAdapter> adapters,
        IPipelineService pipeline,
        ILogger<AdapterPollingJob> logger)
    {
        _adapters = adapters.ToDictionary(a => a.AdapterName, StringComparer.Ordinal);
        _pipeline = pipeline;
        _logger = logger;
    }

    public async Task RunAsync(string adapterName, CancellationToken ct = default)
    {
        if (!_adapters.TryGetValue(adapterName, out var adapter))
        {
            _logger.LogWarning("Adapter '{AdapterName}' not found", adapterName);
            return;
        }

        var correlationId = Guid.NewGuid();
        _logger.LogInformation("Polling adapter {AdapterName} (correlation: {CorrelationId})", adapterName, correlationId);

        var raw = await adapter.FetchAsync(ct);
        if (!raw.Success)
        {
            _logger.LogWarning("Adapter {AdapterName} fetch failed: {Error}", adapterName, raw.ErrorMessage);
            return;
        }

        var candidates = await adapter.NormaliseAsync(raw, ct);
        _logger.LogInformation("Adapter {AdapterName} normalised {Count} candidates", adapterName, candidates.Count);

        await _pipeline.ProcessAsync(candidates, correlationId, ct);
    }
}
