using System.Net;

namespace SchwammyRecovery;

public sealed class WaybackClient
{
    private readonly HttpClient _http;

    public WaybackClient(HttpClient http) => _http = http;

    public async Task<string?> GetHtmlAsync(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _http.GetAsync(url, cancellationToken);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                Console.WriteLine("Wayback returned HTTP 429. Stopping to avoid hammering the archive.");
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"HTTP {(int)response.StatusCode}: {url}");
                return null;
            }

            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Console.WriteLine($"Request failed: {ex.Message}");
            return null;
        }
    }
}
