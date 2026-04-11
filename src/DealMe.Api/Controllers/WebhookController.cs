namespace DealMe.Api.Controllers;

using DealMe.Core.DTOs;
using DealMe.Core.Interfaces.Adapters;
using DealMe.Core.Interfaces.Pipeline;
using DealMe.Infrastructure;
using DealMe.Infrastructure.Jobs;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/webhooks")]
public class WebhookController : ControllerBase
{
    private readonly IEnumerable<IIntegrationAdapter> _adapters;
    private readonly IPipelineService _pipeline;

    private readonly AdapterPollingJob _pollingJob;

    public WebhookController(IEnumerable<IIntegrationAdapter> adapters, IPipelineService pipeline, AdapterPollingJob pollingJob)
    {
        _adapters = adapters;
        _pipeline = pipeline;
        _pollingJob = pollingJob;
    }

    /// <summary>Manually trigger an adapter poll — useful for testing without waiting for Hangfire schedule.</summary>
    [HttpPost("trigger/{adapterName}")]
    public async Task<IActionResult> TriggerAdapter(string adapterName, CancellationToken ct)
    {
        await _pollingJob.RunAsync(adapterName, ct);
        return Accepted(new { adapterName, triggered = true });
    }

    [HttpPost("changedetection")]
    public async Task<IActionResult> ChangeDetection(CancellationToken ct)
    {
        var adapter = _adapters.FirstOrDefault(a => a.AdapterName == AdapterNames.ChangeDetection);
        if (adapter is null) return StatusCode(503, $"{AdapterNames.ChangeDetection} adapter not registered");

        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync(ct);

        var raw = new AdapterResult(
            AdapterName: AdapterNames.ChangeDetection,
            Success: true,
            RawPayload: body,
            ErrorMessage: null,
            FetchedAt: DateTimeOffset.UtcNow);

        var candidates = await adapter.NormaliseAsync(raw, ct);
        await _pipeline.ProcessAsync(candidates, Guid.NewGuid(), ct);

        return Accepted();
    }
}
