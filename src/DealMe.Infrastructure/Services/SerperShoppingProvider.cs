namespace DealMe.Infrastructure.Services;

using System.Net.Http.Json;
using System.Text.Json;
using DealMe.Core.DTOs;
using DealMe.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

public sealed class SerperShoppingProvider : IProductSearchProvider
{
    private readonly HttpClient _http;
    private readonly ILogger<SerperShoppingProvider> _logger;
    private readonly string? _apiKey;

    public string Name => "Serper";

    public SerperShoppingProvider(
        HttpClient http,
        IConfiguration config,
        ILogger<SerperShoppingProvider> logger)
    {
        _http = http;
        _logger = logger;
        _apiKey = config["SERPER_API_KEY"];
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    public async Task<IReadOnlyList<ProductSearchResult>> SearchAsync(string query, CancellationToken ct = default)
    {
        if (!IsConfigured)
            return [];

        var normalized = QueryNormalizer.Normalize(query);

        var request = new HttpRequestMessage(HttpMethod.Post, "https://google.serper.dev/shopping")
        {
            Content = JsonContent.Create(new
            {
                q = normalized,
                gl = "au",     // Australia
                hl = "en",
                num = 30
            })
        };
        request.Headers.Add("X-API-KEY", _apiKey);

        try
        {
            var response = await _http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<SerperResponse>(JsonOpts, ct);
            if (body?.Shopping is null || body.Shopping.Count == 0)
            {
                _logger.LogInformation("Serper: '{Query}' → 0 shopping results", normalized);
                return [];
            }

            var results = body.Shopping
                .Select(s => (item: s, price: ParsePrice(s.Price)))
                .Where(x => x.price > 0 && !string.IsNullOrWhiteSpace(x.item.Title) && !string.IsNullOrWhiteSpace(x.item.Link))
                .Select(x => new ProductSearchResult(
                    x.item.Title!,
                    x.item.Source ?? "Unknown",
                    x.price,
                    x.item.Link!))
                .ToList();

            _logger.LogInformation("Serper: '{Query}' → {Count} results", normalized, results.Count);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Serper search failed for '{Query}', will fall back", normalized);
            return [];
        }
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class SerperResponse
    {
        public List<ShoppingItem>? Shopping { get; set; }
    }

    private sealed class ShoppingItem
    {
        public string? Title { get; set; }
        public string? Source { get; set; }
        public string? Link { get; set; }
        public string? Price { get; set; }
    }

    private static decimal ParsePrice(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return 0;
        // Serper returns "$875.00", "A$1,299.00", "1299.00", etc.
        var cleaned = new string(raw.Where(c => char.IsDigit(c) || c == '.').ToArray());
        return decimal.TryParse(cleaned, out var price) ? price : 0;
    }
}
