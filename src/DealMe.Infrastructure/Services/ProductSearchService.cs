namespace DealMe.Infrastructure.Services;

using DealMe.Core.DTOs;
using Microsoft.Extensions.Logging;

/// <summary>
/// Orchestrates product search across providers.
/// Tries Serper (Google Shopping) first, falls back to StaticIce.
/// </summary>
public sealed class ProductSearchService
{
    private readonly SerperShoppingProvider _serper;
    private readonly StaticIceProvider _staticIce;
    private readonly ILogger<ProductSearchService> _logger;

    public ProductSearchService(
        SerperShoppingProvider serper,
        StaticIceProvider staticIce,
        ILogger<ProductSearchService> logger)
    {
        _serper = serper;
        _staticIce = staticIce;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ProductSearchResult>> SearchAsync(string query, CancellationToken ct = default)
    {
        // Try Serper first if configured
        if (_serper.IsConfigured)
        {
            var results = await _serper.SearchAsync(query, ct);
            if (results.Count > 0)
            {
                _logger.LogInformation("Search '{Query}': {Count} results from Serper", query, results.Count);
                return results;
            }

            _logger.LogInformation("Search '{Query}': Serper returned 0, falling back to StaticIce", query);
        }

        // Fallback to StaticIce
        var fallback = await _staticIce.SearchAsync(query, ct);
        _logger.LogInformation("Search '{Query}': {Count} results from StaticIce", query, fallback.Count);
        return fallback;
    }
}
