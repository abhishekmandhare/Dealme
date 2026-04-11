using DealMe.Infrastructure;
using DealMe.Infrastructure.Jobs;
using DealMe.Infrastructure.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DealMeDbContext>();
    db.Database.Migrate();
    await SeedDefaultChannelAsync(db, app.Configuration);
}

static async Task SeedDefaultChannelAsync(DealMeDbContext db, IConfiguration config)
{
    var defaultUrl = config["DEFAULT_NOTIFICATION_URL"];
    if (string.IsNullOrWhiteSpace(defaultUrl)) return;

    var alreadyExists = await db.NotificationChannels.AnyAsync(c => c.AppriseUrl == defaultUrl);
    if (alreadyExists) return;

    db.NotificationChannels.Add(new DealMe.Core.Domain.Entities.NotificationChannel
    {
        Id = Guid.NewGuid(),
        Type = "discord",
        Name = "Default",
        AppriseUrl = defaultUrl,
        IsEnabled = true,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    });
    await db.SaveChangesAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHangfireDashboard("/hangfire");

RecurringJob.AddOrUpdate<AdapterPollingJob>(
    "ozbargain-poll",
    job => job.RunAsync(AdapterNames.OzBargain, CancellationToken.None),
    "*/5 * * * *");

RecurringJob.AddOrUpdate<AdapterPollingJob>(
    "cheapies-poll",
    job => job.RunAsync(AdapterNames.Cheapies, CancellationToken.None),
    "*/5 * * * *");

RecurringJob.AddOrUpdate<AdapterPollingJob>(
    "frugalfeeds-poll",
    job => job.RunAsync(AdapterNames.FrugalFeeds, CancellationToken.None),
    "*/10 * * * *");

RecurringJob.AddOrUpdate<PriceWatchPollingJob>(
    "pricewatch-poll",
    job => job.RunAsync(CancellationToken.None),
    "0 * * * *"); // every hour

app.MapControllers();
app.Run();
