using System.Text.Json;
using HtmlAgilityPack;

namespace SchwammyRecovery.Recovery;

public interface IWaybackRecoveryService
{
    Task PrefetchArchivePagesAsync(
        CancellationToken cancellationToken = default);

    Task RecoverPostsAsync(
        CancellationToken cancellationToken = default);

    Task RecoverPostAsync(
        string postUrl,
        PostSourceMapEntry? sourceEntry,
        CancellationToken cancellationToken = default);
}

public sealed class WaybackRecoveryService : IWaybackRecoveryService
{
    private readonly PostUrlReader _postUrlReader;
    private readonly WaybackClient _wayback;
    private readonly string _output;
    private readonly Logger _logger;

    public WaybackRecoveryService(
        PostUrlReader postUrlReader,
        WaybackClient wayback,
        string output,
        Logger logger)
    {
        _postUrlReader = postUrlReader;
        _wayback = wayback;
        _output = output;
        _logger = logger;
    }

    public async Task PrefetchArchivePagesAsync(
        CancellationToken cancellationToken = default)
    {
        var postSources = await LoadPostSourceMapAsync();

        var archiveUrls = postSources.Values
            .Where(source => !string.IsNullOrWhiteSpace(source.ArchivePageUrl))
            .Select(source => source.ArchivePageUrl)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (archiveUrls.Count == 0)
        {
            return;
        }

        _logger.Log();
        _logger.Log(
            $"Prefetching {archiveUrls.Count} unique archive page(s) for reuse.");

        foreach (var archiveUrl in archiveUrls)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var archivePagePath = GetArchivePageStoragePath(archiveUrl);

            if (File.Exists(archivePagePath))
            {
                _logger.Log($"  CACHE: {archiveUrl}");
                _logger.Log($"    Existing at: {archivePagePath}");
                continue;
            }

            var html = await _wayback.GetHtmlAsync(
                archiveUrl,
                cancellationToken);

            if (string.IsNullOrWhiteSpace(html))
            {
                _logger.Log($"  CACHE: {archiveUrl}");
                _logger.Log("    Could not retrieve archive page.");
                continue;
            }

            await SaveArchivePageAsync(
                archivePagePath,
                html,
                cancellationToken);

            _logger.Log($"  CACHE: {archiveUrl}");
            _logger.Log($"    Stored at: {archivePagePath}");
        }
    }

    public async Task RecoverPostsAsync(
        CancellationToken cancellationToken = default)
    {
        var postUrlPath = Path.Combine(
            _output,
            "discovery",
            "post-urls.json");

        var postUrls = await _postUrlReader.ReadAsync(
            postUrlPath);

        var postSources = await LoadPostSourceMapAsync();

        _logger.Log();
        _logger.Log(
            $"Found {postUrls.Count} post URL(s) to recover.");

        foreach (var postUrl in postUrls)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sourceEntry = postSources.TryGetValue(
                postUrl,
                out var matchedSource)
                ? matchedSource
                : null;

            await RecoverPostAsync(
                postUrl,
                sourceEntry,
                cancellationToken);
        }

        _logger.Log();
        _logger.Log("Recovery step complete.");
    }

    public async Task RecoverPostAsync(
        string postUrl,
        PostSourceMapEntry? sourceEntry,
        CancellationToken cancellationToken = default)
    {
        var slug = GetSlug(postUrl);

        var postDirectory = Path.Combine(
            _output,
            "recovered",
            slug);

        Directory.CreateDirectory(postDirectory);

        var sourcePath = Path.Combine(
            postDirectory,
            "source.html");

        var capturePath = Path.Combine(
            postDirectory,
            "capture.json");

        if (File.Exists(sourcePath) &&
            File.Exists(capturePath))
        {
            await WriteProvenanceAsync(
                postUrl,
                postDirectory,
                sourceType: "wayback-capture",
                archivePageUrl: sourceEntry?.ArchivePageUrl);

            _logger.Log($"  SKIP: {postUrl}");
            _logger.Log("        Already recovered.");
            return;
        }

        _logger.Log();
        _logger.Log($"  Recovering: {postUrl}");

        var captures = await _wayback.GetCapturesAsync(
            postUrl,
            cancellationToken);

        if (captures is null)
        {
            _logger.Log(
                "  Unable to retrieve HTML captures.");

            var archiveFallbackRecovered = await TryRecoverFromArchivePageAsync(
                postUrl,
                postDirectory,
                sourceEntry,
                cancellationToken);

            if (archiveFallbackRecovered)
            {
                return;
            }

            return;
        }

        if (captures.Count == 0)
        {
            _logger.Log("  No HTML captures found.");

            var archiveFallbackRecovered = await TryRecoverFromArchivePageAsync(
                postUrl,
                postDirectory,
                sourceEntry,
                cancellationToken);

            if (archiveFallbackRecovered)
            {
                return;
            }

            return;
        }

        _logger.Log(
            $"  Found {captures.Count} HTML capture(s).");

        foreach (var capture in captures
                     .OrderByDescending(x => x.Timestamp))
        {
            cancellationToken.ThrowIfCancellationRequested();

            _logger.Log(
                $"  Trying {capture.Timestamp}");

            var html = await _wayback.GetCaptureHtmlAsync(
                capture,
                cancellationToken);

            if (string.IsNullOrWhiteSpace(html))
            {
                _logger.Log("    Not usable.");
                continue;
            }

            var postHtml = ExtractPostHtml(
                html,
                postUrl,
                capture.CaptureUrl);

            if (string.IsNullOrWhiteSpace(postHtml))
            {
                _logger.Log("    Could not isolate the requested post article; using the full capture page.");
                postHtml = html;
            }

            await File.WriteAllTextAsync(
                sourcePath,
                postHtml,
                cancellationToken);

            var json = JsonSerializer.Serialize(
                capture,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });

            await File.WriteAllTextAsync(
                capturePath,
                json,
                cancellationToken);

            await WriteProvenanceAsync(
                postUrl,
                postDirectory,
                sourceType: "wayback-capture",
                archivePageUrl: sourceEntry?.ArchivePageUrl);

            _logger.Log("  SUCCESS");
            _logger.Log($"    HTML:    {sourcePath}");
            _logger.Log($"    Capture: {capturePath}");
            _logger.Log($"    Provenance: {Path.Combine(postDirectory, "provenance.json")}");

            return;
        }

        _logger.Log(
            "  FAILED: No usable capture found.");

        var archiveRecovered = await TryRecoverFromArchivePageAsync(
            postUrl,
            postDirectory,
            sourceEntry,
            cancellationToken);

        if (archiveRecovered)
        {
            return;
        }
    }

    private async Task<Dictionary<string, PostSourceMapEntry>> LoadPostSourceMapAsync()
    {
        var sourceMapPath = Path.Combine(
            _output,
            "discovery",
            "post-source-map.json");

        if (!File.Exists(sourceMapPath))
            return new Dictionary<string, PostSourceMapEntry>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var json = await File.ReadAllTextAsync(sourceMapPath);

            if (string.IsNullOrWhiteSpace(json))
                return new Dictionary<string, PostSourceMapEntry>(StringComparer.OrdinalIgnoreCase);

            var existing = JsonSerializer.Deserialize<Dictionary<string, PostSourceMapEntry>>(json);

            return existing ?? new Dictionary<string, PostSourceMapEntry>(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.Log($"  WARNING: Could not load discovery/post-source-map.json: {ex.Message}");
            return new Dictionary<string, PostSourceMapEntry>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private async Task<bool> TryRecoverFromArchivePageAsync(
        string postUrl,
        string postDirectory,
        PostSourceMapEntry? sourceEntry,
        CancellationToken cancellationToken)
    {
        if (sourceEntry is null ||
            string.IsNullOrWhiteSpace(sourceEntry.ArchivePageUrl))
        {
            _logger.Log(
                "  No archive page URL is available for this post.");
            return false;
        }

        var archivePagePath = GetArchivePageStoragePath(
            sourceEntry.ArchivePageUrl);

        var sourcePath = Path.Combine(
            postDirectory,
            "source.html");

        var capturePath = Path.Combine(
            postDirectory,
            "capture.json");

        var html = await LoadArchivePageHtmlAsync(
            sourceEntry.ArchivePageUrl,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(html))
        {
            _logger.Log(
                "  Archive page could not be retrieved.");
            return false;
        }

        var postHtml = ExtractPostHtml(
            html,
            postUrl,
            sourceEntry.ArchivePageUrl);

        if (string.IsNullOrWhiteSpace(postHtml))
        {
            _logger.Log(
                "  Archive page did not contain the requested post.");
            return false;
        }

        await File.WriteAllTextAsync(
            sourcePath,
            postHtml,
            cancellationToken);

        var archiveCapture = new WaybackCapture
        {
            Timestamp = "archive-page-fallback",
            OriginalUrl = postUrl,
            CaptureUrl = sourceEntry.ArchivePageUrl,
            StatusCode = "200",
            MimeType = "text/html",
            Digest = null
        };

        var json = JsonSerializer.Serialize(
            archiveCapture,
            new JsonSerializerOptions
            {
                WriteIndented = true
            });

        await File.WriteAllTextAsync(
            capturePath,
            json,
            cancellationToken);

        await WriteProvenanceAsync(
            postUrl,
            postDirectory,
            sourceType: "archive-page",
            archivePageUrl: sourceEntry.ArchivePageUrl,
            archivePagePath: archivePagePath,
            notes: "Recovered from the archive page because no usable Wayback capture was available.");

        _logger.Log("  FALLBACK: Used saved archive page HTML.");
        _logger.Log($"    HTML:    {sourcePath}");
        _logger.Log($"    Capture: {capturePath}");
        _logger.Log($"    Provenance: {Path.Combine(postDirectory, "provenance.json")}");

        return true;
    }

    private async Task<string?> LoadArchivePageHtmlAsync(
        string archiveUrl,
        CancellationToken cancellationToken)
    {
        var archivePagePath = GetArchivePageStoragePath(archiveUrl);

        if (File.Exists(archivePagePath))
        {
            return await File.ReadAllTextAsync(
                archivePagePath,
                cancellationToken);
        }

        var html = await _wayback.GetHtmlAsync(
            archiveUrl,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        await SaveArchivePageAsync(
            archivePagePath,
            html,
            cancellationToken);

        return html;
    }

    private static string? ExtractPostHtml(
        string html,
        string postUrl,
        string archivePageUrl)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);

        foreach (var article in document.DocumentNode
                     .SelectNodes("//article")
                     ?? Enumerable.Empty<HtmlNode>())
        {
            if (ArticleContainsPostUrl(article, postUrl, archivePageUrl))
            {
                return article.OuterHtml;
            }
        }

        return null;
    }

    private static bool ArticleContainsPostUrl(
        HtmlNode article,
        string postUrl,
        string archivePageUrl)
    {
        foreach (var link in article
                     .SelectNodes(".//a[@href]")
                     ?? Enumerable.Empty<HtmlNode>())
        {
            var href = link.GetAttributeValue("href", string.Empty);
            if (string.IsNullOrWhiteSpace(href))
            {
                continue;
            }

            var resolvedUrl = ResolveUrl(href, archivePageUrl);
            if (string.IsNullOrWhiteSpace(resolvedUrl))
            {
                continue;
            }

            if (UrlsMatch(resolvedUrl, postUrl))
            {
                return true;
            }
        }

        return false;
    }

    private static string? ResolveUrl(
        string href,
        string archivePageUrl)
    {
        if (Uri.TryCreate(href, UriKind.Absolute, out var absolute))
        {
            return ExtractOriginalUrl(absolute.AbsoluteUri)
                ?? absolute.AbsoluteUri;
        }

        if (Uri.TryCreate(archivePageUrl, UriKind.Absolute, out var baseUri))
        {
            var resolved = new Uri(baseUri, href).AbsoluteUri;
            return ExtractOriginalUrl(resolved)
                ?? resolved;
        }

        return null;
    }

    private static bool UrlsMatch(
        string candidate,
        string postUrl)
    {
        var normalizedCandidate = NormalizeUrl(candidate);
        var normalizedPostUrl = NormalizeUrl(postUrl);

        return string.Equals(
            normalizedCandidate,
            normalizedPostUrl,
            StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return Uri.UnescapeDataString(url).TrimEnd('/');
        }

        var absolute = uri.GetLeftPart(UriPartial.Authority) + uri.PathAndQuery;
        return Uri.UnescapeDataString(absolute).TrimEnd('/');
    }

    private async Task SaveArchivePageAsync(
        string archivePagePath,
        string html,
        CancellationToken cancellationToken)
    {
        var targetDirectory = Path.GetDirectoryName(archivePagePath);

        if (!string.IsNullOrWhiteSpace(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }

        await File.WriteAllTextAsync(
            archivePagePath,
            html,
            cancellationToken);
    }

    private string GetArchivePageStoragePath(string archiveUrl)
    {
        var relativePath = GetArchivePageRelativePath(archiveUrl);
        return Path.Combine(_output, relativePath);
    }

    private static string GetArchivePageRelativePath(string archiveUrl)
    {
        var original = ExtractOriginalUrl(archiveUrl);

        if (original is null)
            return Path.Combine("archive-pages", "unknown", "index.html");

        var uri = new Uri(original);
        var path = uri.AbsolutePath.Trim('/');

        var segments = string.IsNullOrWhiteSpace(path)
            ? []
            : path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length < 2)
            return Path.Combine("archive-pages", "unknown", "index.html");

        var year = segments[0];
        var month = segments[1];

        var root = Path.Combine("archive-pages", year, month);

        if (segments.Length >= 4 &&
            segments[2].Equals("page", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(segments[3], out var pageNumber) &&
            pageNumber > 0)
        {
            return Path.Combine(root, $"page-{pageNumber}", "index.html");
        }

        return Path.Combine(root, "page-1", "index.html");
    }

    private static string? ExtractOriginalUrl(string url)
    {
        if (!IsWaybackUrl(url))
            return url;

        var uri = new Uri(url);

        var path = uri.AbsolutePath;
        var marker = "/web/";
        var index = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);

        if (index < 0)
            return null;

        var remainder = path[(index + marker.Length)..];
        var slash = remainder.IndexOf('/');

        if (slash < 0)
            return null;

        var original = remainder[(slash + 1)..];
        original = Uri.UnescapeDataString(original);

        if (!original.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !original.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            original = "https://" + original;
        }

        return original;
    }

    private static bool IsWaybackUrl(string url) =>
        new Uri(url).Host.Equals("web.archive.org", StringComparison.OrdinalIgnoreCase);

    private async Task WriteProvenanceAsync(
        string postUrl,
        string postDirectory,
        string sourceType,
        string? archivePageUrl = null,
        string? archivePagePath = null,
        string? notes = null)
    {
        var provenancePath = Path.Combine(
            postDirectory,
            "provenance.json");

        RecoveryProvenance? existing = null;

        if (File.Exists(provenancePath))
        {
            try
            {
                var existingJson = await File.ReadAllTextAsync(provenancePath);

                if (!string.IsNullOrWhiteSpace(existingJson))
                {
                    existing = JsonSerializer.Deserialize<RecoveryProvenance>(existingJson);
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"  WARNING: Could not load existing provenance.json: {ex.Message}");
            }
        }

        var provenance = existing ?? new RecoveryProvenance();

        provenance.PostUrl = postUrl;
        provenance.SourceType = string.IsNullOrWhiteSpace(sourceType)
            ? provenance.SourceType
            : sourceType;
        provenance.ArchivePageUrl = string.IsNullOrWhiteSpace(archivePageUrl)
            ? provenance.ArchivePageUrl
            : archivePageUrl;
        provenance.ArchivePagePath = string.IsNullOrWhiteSpace(archivePagePath)
            ? provenance.ArchivePagePath
            : archivePagePath;

        if (provenance.RecoveredAtUtc == default)
        {
            provenance.RecoveredAtUtc = DateTimeOffset.UtcNow;
        }

        provenance.Notes = notes ??
            provenance.Notes ??
            "Recovered from a Wayback capture for the original post URL.";

        var json = JsonSerializer.Serialize(
            provenance,
            new JsonSerializerOptions
            {
                WriteIndented = true
            });

        await File.WriteAllTextAsync(
            provenancePath,
            json,
            CancellationToken.None);
    }

    private static string GetSlug(string postUrl)
    {
        var uri = new Uri(postUrl);

        return uri.AbsolutePath
            .Trim('/')
            .Split(
                '/',
                StringSplitOptions.RemoveEmptyEntries)
            .Last();
    }
}
