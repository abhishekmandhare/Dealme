namespace DealMe.Infrastructure;

using DealMe.Core.Interfaces.Adapters;
using DealMe.Core.Interfaces.Notifications;
using DealMe.Core.Interfaces.Pipeline;
using DealMe.Infrastructure.Adapters;
using DealMe.Infrastructure.Persistence;
using DealMe.Infrastructure.Services;
using Hangfire;
using Hangfire.InMemory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration config)
    {
        var provider = config["DATABASE_PROVIDER"] ?? "sqlite";
        var connStr = config["CONNECTION_STRING"]
            ?? throw new InvalidOperationException("CONNECTION_STRING env var is required.");

        services.AddDbContext<DealMeDbContext>(opts =>
        {
            if (provider.Equals("postgresql", StringComparison.OrdinalIgnoreCase))
                opts.UseNpgsql(connStr);
            else
                opts.UseSqlite(connStr);
        });

        // Adapters hold mutable _lastStatus — register as Singleton.
        // IHttpClientFactory is registered by AddHttpClient and is safe to use from singletons.
        services.AddHttpClient();
        services.AddSingleton<OzBargainAdapter>(sp =>
            new OzBargainAdapter(sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(OzBargainAdapter)),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<OzBargainAdapter>>()));
        services.AddSingleton<CheapiesAdapter>(sp =>
            new CheapiesAdapter(sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(CheapiesAdapter)),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<CheapiesAdapter>>()));
        services.AddSingleton<FrugalFeedsAdapter>(sp =>
            new FrugalFeedsAdapter(sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(FrugalFeedsAdapter)),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<FrugalFeedsAdapter>>()));
        services.AddSingleton<ChangeDetectionAdapter>();
        services.AddSingleton<PointHacksAdapter>(sp =>
            new PointHacksAdapter(sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(PointHacksAdapter)),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<PointHacksAdapter>>()));
        services.AddSingleton<IIntegrationAdapter>(sp => sp.GetRequiredService<OzBargainAdapter>());
        services.AddSingleton<IIntegrationAdapter>(sp => sp.GetRequiredService<CheapiesAdapter>());
        services.AddSingleton<IIntegrationAdapter>(sp => sp.GetRequiredService<FrugalFeedsAdapter>());
        services.AddSingleton<IIntegrationAdapter>(sp => sp.GetRequiredService<ChangeDetectionAdapter>());
        services.AddSingleton<IIntegrationAdapter>(sp => sp.GetRequiredService<PointHacksAdapter>());

        services.AddScoped<IPipelineService, PipelineService>();
        services.AddHttpClient<AppriseClient>();
        services.AddScoped<INotificationService, NotificationService>();

        // Product search: Serper (Google Shopping) → StaticIce fallback
        services.AddHttpClient<SerperShoppingProvider>();
        services.AddHttpClient<StaticIceProvider>();
        services.AddScoped<ProductSearchService>();

        services.AddScoped<Jobs.AdapterPollingJob>();
        services.AddScoped<Jobs.PriceWatchPollingJob>();

        services.AddHangfire(hf => hf.UseInMemoryStorage());
        services.AddHangfireServer();

        return services;
    }
}
