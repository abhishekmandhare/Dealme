namespace DealMe.Api.Controllers;

using System.Text.Json;
using DealMe.Core.Domain.Enums;
using DealMe.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

public sealed record UpdateStatusRequest(OpportunityStatus Status);

[ApiController]
[Route("api/[controller]")]
public class OpportunitiesController : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);
    private readonly DealMeDbContext _db;

    public OpportunitiesController(DealMeDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var rows = await _db.Opportunities
            .OrderByDescending(o => o.CreatedAt)
            .Take(100)
            .ToListAsync(ct);

        var result = rows.Select(o => new
        {
            o.Id,
            o.Source,
            o.SourceId,
            o.Type,
            o.Status,
            o.Title,
            o.Description,
            o.Value,
            o.OriginalValue,
            o.DiscountPercent,
            o.Url,
            o.ImageUrl,
            o.ExpiresAt,
            o.Confidence,
            Tags     = Deserialize<string[]>(o.Tags) ?? [],
            Metadata = Deserialize<Dictionary<string, JsonElement>>(o.Metadata) ?? [],
            o.CreatedAt,
            o.UpdatedAt,
        });

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var o = await _db.Opportunities.FindAsync([id], ct);
        if (o is null) return NotFound();

        return Ok(new
        {
            o.Id,
            o.Source,
            o.SourceId,
            o.Type,
            o.Status,
            o.Title,
            o.Description,
            o.Value,
            o.OriginalValue,
            o.DiscountPercent,
            o.Url,
            o.ImageUrl,
            o.ExpiresAt,
            o.Confidence,
            Tags     = Deserialize<string[]>(o.Tags) ?? [],
            Metadata = Deserialize<Dictionary<string, JsonElement>>(o.Metadata) ?? [],
            o.CreatedAt,
            o.UpdatedAt,
        });
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateStatusRequest body, CancellationToken ct)
    {
        var o = await _db.Opportunities.FindAsync([id], ct);
        if (o is null) return NotFound();
        o.Status = body.Status;
        o.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private T? Deserialize<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return default;
        try { return JsonSerializer.Deserialize<T>(json, JsonOpts); }
        catch { return default; }
    }
}
