namespace DealMe.Api.Controllers;

using DealMe.Core.Interfaces.Adapters;
using DealMe.Infrastructure.Jobs;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class AdaptersController : ControllerBase
{
    private readonly IEnumerable<IIntegrationAdapter> _adapters;
    private readonly AdapterPollingJob _pollingJob;

    public AdaptersController(IEnumerable<IIntegrationAdapter> adapters, AdapterPollingJob pollingJob)
    {
        _adapters = adapters;
        _pollingJob = pollingJob;
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(CancellationToken ct)
    {
        var statuses = await Task.WhenAll(_adapters.Select(a => a.GetStatusAsync(ct)));
        return Ok(statuses);
    }

    [HttpPost("{name}/run")]
    public async Task<IActionResult> ForceRun(string name, CancellationToken ct)
    {
        var adapter = _adapters.FirstOrDefault(a =>
            string.Equals(a.AdapterName, name, StringComparison.OrdinalIgnoreCase));
        if (adapter is null) return NotFound();

        await _pollingJob.RunAsync(adapter.AdapterName, ct);

        var status = await adapter.GetStatusAsync(ct);
        return Ok(status);
    }
}
