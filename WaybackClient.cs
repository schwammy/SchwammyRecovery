using System.Net;
using System.Text.Json;

namespace SchwammyRecovery;

public sealed class WaybackClient
{
    private readonly HttpClient _http;

    public WaybackClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<string?> GetHtmlAsync(
        string url,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response =
                await _http.GetAsync(url, cancellationToken);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                Console.WriteLine(
                    "Wayback returned HTTP 429. " +
                    "Stopping to avoid hammering the archive.");

                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine(
                    $"HTTP {(int)response.StatusCode}: {url}");

                return null;
            }

            return await response.Content.ReadAsStringAsync(
                cancellationToken);
        }
        catch (Exception ex) when (
            ex is HttpRequestException or TaskCanceledException)
        {
            Console.WriteLine(
                $"Request failed: {ex.Message}");

            return null;
        }
    }

    public async Task<IReadOnlyList<WaybackCapture>> GetCapturesAsync(
        string originalUrl,
        CancellationToken cancellationToken = default)
    {
        var encodedUrl =
            Uri.EscapeDataString(originalUrl);

        var cdxUrl =
            "https://web.archive.org/cdx/search/cdx" +
            $"?url={encodedUrl}" +
            "&output=json" +
            "&filter=statuscode:200" +
            "&filter=mimetype:text/html" +
            "&fl=timestamp,original,statuscode,mimetype,digest" +
            "&collapse=digest";

        var json = await GetHtmlAsync(
            cdxUrl,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(json))
            return [];

        using var document =
            JsonDocument.Parse(json);

        var rows = document.RootElement;

        if (rows.ValueKind != JsonValueKind.Array ||
            rows.GetArrayLength() < 2)
        {
            return [];
        }

        var captures = new List<WaybackCapture>();

        // First row contains the column names.
        for (var i = 1; i < rows.GetArrayLength(); i++)
        {
            var row = rows[i];

            if (row.ValueKind != JsonValueKind.Array ||
                row.GetArrayLength() < 5)
            {
                continue;
            }

            var timestamp = row[0].GetString();
            var original = row[1].GetString();
            var status = row[2].GetString();
            var mimeType = row[3].GetString();
            var digest = row[4].GetString();

            if (string.IsNullOrWhiteSpace(timestamp) ||
                string.IsNullOrWhiteSpace(original))
            {
                continue;
            }

            var captureUrl =
                $"https://web.archive.org/web/" +
                $"{timestamp}/{original}";

            captures.Add(new WaybackCapture
            {
                Timestamp = timestamp,
                OriginalUrl = original,
                CaptureUrl = captureUrl,
                StatusCode = status,
                MimeType = mimeType,
                Digest = digest
            });
        }

        return captures;
    }

    public async Task<string?> GetCaptureHtmlAsync(
        WaybackCapture capture,
        CancellationToken cancellationToken = default)
    {
        return await GetHtmlAsync(
            capture.CaptureUrl,
            cancellationToken);
    }
}