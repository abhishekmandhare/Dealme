namespace DealMe.Infrastructure.Services;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

public sealed class AppriseClient
{
    private readonly HttpClient _http;
    private readonly string _appriseUrl;
    private readonly ILogger<AppriseClient> _logger;

    public AppriseClient(HttpClient http, IConfiguration config, ILogger<AppriseClient> logger)
    {
        _http = http;
        _appriseUrl = (config["APPRISE_URL"] ?? "http://apprise:8000").TrimEnd('/');
        _logger = logger;
    }

    /// <summary>
    /// POST to Apprise API /notify endpoint.
    /// Apprise API payload: { "title": "...", "body": "...", "urls": ["discord://..."] }
    /// </summary>
    public async Task PostAsync(
        string title,
        string body,
        IReadOnlyList<string> appriseUrls,
        CancellationToken ct = default)
    {
        if (appriseUrls.Count == 0)
        {
            _logger.LogWarning("AppriseClient: no URLs provided, skipping notification");
            return;
        }

        var payload = new
        {
            title,
            body,
            urls = appriseUrls
        };

        var json = JsonSerializer.Serialize(payload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _http.PostAsync($"{_appriseUrl}/notify", content, ct);
            if (response.IsSuccessStatusCode)
                _logger.LogInformation("Notification sent: {Title}", title);
            else
            {
                var responseBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning(
                    "Apprise returned {Status} for '{Title}': {Body}",
                    (int)response.StatusCode, title, responseBody);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send notification: {Title}", title);
        }
    }
}
