namespace DealMe.Api.Controllers;

using DealMe.Core.Domain.Entities;
using DealMe.Infrastructure.Jobs;
using DealMe.Infrastructure.Persistence;
using DealMe.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

public sealed record CreatePriceWatchRequest(string Name, string SearchQuery, decimal TargetPrice);

[ApiController]
[Route("api/[controller]")]
public class PriceWatchesController : ControllerBase
{
    private readonly DealMeDbContext _db;
    private readonly ProductSearchService _search;
    private readonly PriceWatchPollingJob _pollingJob;

    public PriceWatchesController(DealMeDbContext db, ProductSearchService search, PriceWatchPollingJob pollingJob)
    {
        _db = db;
        _search = search;
        _pollingJob = pollingJob;
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q)) return BadRequest("q is required");
        var results = await _search.SearchAsync(q, ct);
        return Ok(results);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(await _db.PriceWatches.OrderByDescending(p => p.CreatedAt).ToListAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var watch = await _db.PriceWatches.FindAsync([id], ct);
        return watch is null ? NotFound() : Ok(watch);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePriceWatchRequest req, CancellationToken ct)
    {
        var watch = new PriceWatch
        {
            Id          = Guid.NewGuid(),
            Name        = req.Name,
            SearchQuery = req.SearchQuery,
            TargetPrice = req.TargetPrice,
            IsActive    = true,
            CreatedAt   = DateTimeOffset.UtcNow,
            UpdatedAt   = DateTimeOffset.UtcNow,
        };
        _db.PriceWatches.Add(watch);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetById), new { id = watch.Id }, watch);
    }

    [HttpPost("{id:guid}/check")]
    public async Task<IActionResult> ForceCheck(Guid id, CancellationToken ct)
    {
        var watch = await _db.PriceWatches.FindAsync([id], ct);
        if (watch is null) return NotFound();

        var appriseUrls = await _db.NotificationChannels
            .Where(c => c.IsEnabled)
            .Select(c => c.AppriseUrl)
            .ToListAsync(ct);

        await _pollingJob.CheckSingleAsync(watch, appriseUrls, ct);
        await _db.SaveChangesAsync(ct);

        return Ok(watch);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var watch = await _db.PriceWatches.FindAsync([id], ct);
        if (watch is null) return NotFound();
        _db.PriceWatches.Remove(watch);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
