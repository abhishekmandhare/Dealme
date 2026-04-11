namespace DealMe.Api.Controllers;

using DealMe.Core.Domain.Entities;
using DealMe.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/[controller]")]
public class SettingsController : ControllerBase
{
    private readonly DealMeDbContext _db;

    public SettingsController(DealMeDbContext db) => _db = db;

    [HttpGet("preferences")]
    public async Task<IActionResult> GetPreferences(CancellationToken ct)
    {
        var prefs = await _db.UserPreferences.FirstOrDefaultAsync(ct);
        if (prefs is null)
        {
            prefs = new UserPreferences
            {
                Id = Guid.NewGuid(),
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _db.UserPreferences.Add(prefs);
            await _db.SaveChangesAsync(ct);
        }
        return Ok(prefs);
    }

    [HttpPut("preferences")]
    public async Task<IActionResult> UpdatePreferences([FromBody] UserPreferences updated, CancellationToken ct)
    {
        var prefs = await _db.UserPreferences.FirstOrDefaultAsync(ct);
        if (prefs is null) return NotFound();

        prefs.MinDealValue = updated.MinDealValue;
        prefs.QuietHoursStart = updated.QuietHoursStart;
        prefs.QuietHoursEnd = updated.QuietHoursEnd;
        prefs.Timezone = updated.Timezone;
        prefs.EnabledCategories = updated.EnabledCategories;
        prefs.InterestTags = updated.InterestTags;
        prefs.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Ok(prefs);
    }

    [HttpGet("channels")]
    public async Task<IActionResult> GetChannels(CancellationToken ct)
        => Ok(await _db.NotificationChannels.ToListAsync(ct));

    [HttpPost("channels")]
    public async Task<IActionResult> CreateChannel([FromBody] NotificationChannel channel, CancellationToken ct)
    {
        channel.Id = Guid.NewGuid();
        channel.CreatedAt = DateTimeOffset.UtcNow;
        channel.UpdatedAt = DateTimeOffset.UtcNow;
        _db.NotificationChannels.Add(channel);
        await _db.SaveChangesAsync(ct);
        return Created($"api/settings/channels/{channel.Id}", channel);
    }

    [HttpDelete("channels/{id:guid}")]
    public async Task<IActionResult> DeleteChannel(Guid id, CancellationToken ct)
    {
        var channel = await _db.NotificationChannels.FindAsync([id], ct);
        if (channel is null) return NotFound();
        _db.NotificationChannels.Remove(channel);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
