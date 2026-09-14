using System.Text.Json;
using HtmlAgilityPack;

namespace SchwammyRecovery;

public sealed class ArchiveCrawler
{
    private readonly WaybackClient _client;
    private readonly string _output;
    private readonly HashSet<string> _visitedArchivePages = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _postUrls = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PostSourceMapEntry> _postSources = new(StringComparer.OrdinalIgnoreCase);
    private readonly Logger _logger;
    public ArchiveCrawler(WaybackClient client, string output, Logger logger)
    {
        _client = client;
        _output = output;
        _logger = logger;
        Directory.CreateDirectory(_output);
    }

    public async Task CrawlArchiveAsync(
        string startUrl,
        CancellationToken cancellationToken = default)
    {
        _postUrls.Clear();
        _visitedArchivePages.Clear();
        _postSources.Clear();

        var existingPostUrls = await LoadExistingPostUrlsAsync();
        var existingPostSources = await LoadExistingPostSourceMapAsync();

        foreach (var kvp in existingPostSources)
        {
            _postSources[kvp.Key] = kvp.Value;
        }

        var existingCount = existingPostUrls.Count;
        var discoveredThisRun = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        _logger.Log($"Starting at:\n  {startUrl}\n");

        // Phase 1: crawl the supplied monthly archive and its pagination.
        await CrawlArchivePageAsync(startUrl, discoveredThisRun, cancellationToken);

        var posts = _postUrls
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        await File.WriteAllTextAsync(
            Path.Combine(_output, "post-urls.json"),
            JsonSerializer.Serialize(posts, new JsonSerializerOptions { WriteIndented = true }));

        await SavePostSourceMapAsync();

        var alreadyExistingCount = discoveredThisRun
            .Count(url => existingPostUrls.Contains(url));

        var addedCount = discoveredThisRun.Count - alreadyExistingCount;

        _logger.Log(
            $"\nThis crawl found {discoveredThisRun.Count} URL(s) in the target month. " +
            $"Already present in the cumulative post-urls.json: {alreadyExistingCount}. " +
            $"Newly added this run: {addedCount}. " +
            $"Total unique URLs now in file: {posts.Length}.");
    }

    private async Task<HashSet<string>> LoadExistingPostUrlsAsync()
    {
        var path = Path.Combine(_output, "post-urls.json");
        var existingPostUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!File.Exists(path))
            return existingPostUrls;

        try
        {
            var json = await File.ReadAllTextAsync(path);

            if (string.IsNullOrWhiteSpace(json))
                return existingPostUrls;

            var existing = JsonSerializer.Deserialize<string[]>(json);

            if (existing is null)
                return existingPostUrls;

            foreach (var postUrl in existing)
            {
                if (!string.IsNullOrWhiteSpace(postUrl) &&
                    LooksLikePostUrl(postUrl) &&
                    !LooksLikeLegacyNonPostUrl(postUrl))
                {
                    existingPostUrls.Add(postUrl);
                    _postUrls.Add(postUrl);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Log($"  WARNING: Could not load existing post-urls.json: {ex.Message}");
        }

        return existingPostUrls;
    }

    private async Task<Dictionary<string, PostSourceMapEntry>> LoadExistingPostSourceMapAsync()
    {
        var path = Path.Combine(_output, "post-source-map.json");

        if (!File.Exists(path))
            return new Dictionary<string, PostSourceMapEntry>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var json = await File.ReadAllTextAsync(path);

            if (string.IsNullOrWhiteSpace(json))
                return new Dictionary<string, PostSourceMapEntry>(StringComparer.OrdinalIgnoreCase);

            var existing = JsonSerializer.Deserialize<Dictionary<string, PostSourceMapEntry>>(json);

            return existing ?? new Dictionary<string, PostSourceMapEntry>(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.Log($"  WARNING: Could not load existing post-source-map.json: {ex.Message}");
            return new Dictionary<string, PostSourceMapEntry>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private async Task SavePostSourceMapAsync()
    {
        var path = Path.Combine(_output, "post-source-map.json");

        var json = JsonSerializer.Serialize(
            _postSources,
            new JsonSerializerOptions { WriteIndented = true });

        await File.WriteAllTextAsync(path, json);
    }

    private async Task CrawlArchivePageAsync(
        string archiveUrl,
        HashSet<string> discoveredThisRun,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        archiveUrl = NormalizeArchiveUrl(archiveUrl);

        if (!_visitedArchivePages.Add(archiveUrl))
            return;

        _logger.Log();
        _logger.Log($"==================================================");
        _logger.Log($"Archive: {archiveUrl}");
        _logger.Log($"==================================================");

        var html = await _client.GetHtmlAsync(archiveUrl);

        if (html is null)
        {
            _logger.Log($"  ERROR: Could not retrieve archive page.");
            return;
        }

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        foreach (var link in doc.DocumentNode.SelectNodes("//a[@href]")
                 ?? Enumerable.Empty<HtmlNode>())
        {
            var href = link.GetAttributeValue("href", "");

            if (string.IsNullOrWhiteSpace(href))
                continue;

            var absolute = Resolve(href, archiveUrl);

            if (absolute is null)
                continue;

            absolute = NormalizeArchiveUrl(absolute);

            if (!IsWaybackUrl(absolute))
                continue;

            var original = ExtractOriginalUrl(absolute);

            if (original is null)
                continue;

            // Is this a link to another page of the same monthly archive?
            if (LooksLikeArchivePagination(original))
            {
                if (SameOriginalMonth(archiveUrl, absolute))
                {
                    _logger.Log(
                        $"  Following pagination: {absolute}");

                    await CrawlArchivePageAsync(absolute, discoveredThisRun, cancellationToken);
                }

                continue;
            }

            // Is this a link back to Schwammy Says?
            if (!IsOriginalSchwammyUrl(original))
                continue;

            if (!LooksLikePostUrl(original))
                continue;

            if (!IsPostPermalink(link))
                continue;

            discoveredThisRun.Add(original);

            _postSources[original] = new PostSourceMapEntry
            {
                PostUrl = original,
                ArchivePageUrl = archiveUrl
            };

            if (_postUrls.Add(original))
            {
                _logger.Log(
                    $"  POST [{GetArchiveDescription(archiveUrl)}]: {original}");
            }
        }
    }

    private static string GetArchiveDescription(string archiveUrl)
    {
        var original = ExtractOriginalUrl(archiveUrl);

        if (original is null)
            return archiveUrl;

        var uri = new Uri(original);

        return uri.AbsolutePath.Trim('/');
    }

    private async Task SaveArchivePageAsync(
        string archiveUrl,
        string html)
    {
        var targetPath = GetArchivePageStoragePath(archiveUrl);
        var targetDirectory = Path.GetDirectoryName(targetPath);

        if (!string.IsNullOrWhiteSpace(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }

        await File.WriteAllTextAsync(targetPath, html);
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
            return Path.Combine("archive-pages", "unknown.html");

        var uri = new Uri(original);
        var path = uri.AbsolutePath.Trim('/');

        var segments = string.IsNullOrWhiteSpace(path)
            ? []
            : path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        var relativePath = "archive-pages";

        foreach (var segment in segments)
        {
            relativePath = Path.Combine(relativePath, segment);
        }

        return Path.Combine(relativePath, "index.html");
    }
    private static bool LooksLikeArchivePagination(string url)
    {
        return url.Contains("/page/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikePostUrl(string url)
    {
        var uri = new Uri(url);
        var path = uri.AbsolutePath.Trim('/');

        if (string.IsNullOrEmpty(path))
            return false;

        // Definitely not posts.
        string[] excluded = {
        "wp-admin",
        "wp-content",
        "wp-includes",
        "feed",
        "category",
        "tag",
        "author",
        "page",
        "search",
        "comments",
        "about",
        "contact"
    };

        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        var first = parts.FirstOrDefault();

        if (first is null)
            return false;

        if (excluded.Contains(first, StringComparer.OrdinalIgnoreCase))
            return false;

        // Explicitly exclude wp-login.php and similar WordPress files.
        if (first.Equals("wp-login.php", StringComparison.OrdinalIgnoreCase))
            return false;

        // Legacy category pages can look like post URLs but are not posts.
        if (path.Contains(",category,", StringComparison.OrdinalIgnoreCase))
            return false;

        // WordPress date archives:
        //   /2007/03/
        //   /2007/03/page/2/
        if (parts.Length >= 2 &&
            int.TryParse(parts[0], out var year) &&
            int.TryParse(parts[1], out var month) &&
            year >= 2000 &&
            year <= 2100 &&
            month >= 1 &&
            month <= 12)
        {
            return false;
        }

        // Anything explicitly containing /page/ is pagination.
        if (path.Contains("/page/", StringComparison.OrdinalIgnoreCase))
            return false;

        // Don't treat images, scripts, stylesheets, etc. as posts.
        string[] excludedExtensions = {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp",
        ".svg", ".ico",
        ".css", ".js",
        ".xml", ".json",
        ".pdf", ".zip"
    };

        if (excludedExtensions.Any(ext =>
            path.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return true;
    }

    private static bool IsPostPermalink(HtmlNode link)
    {
        var rel = link.GetAttributeValue("rel", string.Empty);

        if (rel.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Contains("bookmark", StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        return link.SelectSingleNode(
                   "ancestor::*[self::h1 or self::h2 or self::h3][contains(concat(' ', normalize-space(@class), ' '), ' entry-title ')]")
               is not null;
    }

    private static bool LooksLikeLegacyNonPostUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return true;

        var path = uri.AbsolutePath;

        return path.EndsWith(".avi", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".aspx", StringComparison.OrdinalIgnoreCase) ||
               path.Contains(",category,", StringComparison.OrdinalIgnoreCase);
    }

    private static bool SameOriginalMonth(string current, string candidate)
    {
        var a = ExtractOriginalUrl(current);
        var b = ExtractOriginalUrl(candidate);
        if (a is null || b is null) return false;

        var ua = new Uri(a);
        var ub = new Uri(b);

        var aParts = ua.AbsolutePath.Trim('/').Split('/');
        var bParts = ub.AbsolutePath.Trim('/').Split('/');

        return aParts.Length >= 2 &&
               bParts.Length >= 2 &&
               int.TryParse(aParts[0], out var ay) &&
               int.TryParse(aParts[1], out var am) &&
               int.TryParse(bParts[0], out var by) &&
               int.TryParse(bParts[1], out var bm) &&
               ay == by && am == bm;
    }

    private sealed class PostSourceMapEntry
    {
        public string PostUrl { get; set; } = string.Empty;
        public string ArchivePageUrl { get; set; } = string.Empty;
    }

    private static bool IsWaybackUrl(string url) =>
        new Uri(url).Host.Equals("web.archive.org", StringComparison.OrdinalIgnoreCase);

    private static bool IsOriginalSchwammyUrl(string url) =>
        new Uri(url).Host.Equals("www.schwammysays.net", StringComparison.OrdinalIgnoreCase) ||
        new Uri(url).Host.Equals("schwammysays.net", StringComparison.OrdinalIgnoreCase);

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

        // Example:
        // 20220925020544/http://www.schwammysays.net/foo/
        //
        // Remove the Wayback timestamp.
        var slash = remainder.IndexOf('/');

        if (slash < 0)
            return null;

        var original = remainder[(slash + 1)..];

        // The original URL is encoded in the Wayback path.
        original = Uri.UnescapeDataString(original);

        if (!original.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !original.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            original = "https://" + original;
        }

        return original;
    }

    private static string? Resolve(string href, string baseUrl)
    {
        if (Uri.TryCreate(new Uri(baseUrl), href, out var result))
            return result.ToString();

        return null;
    }

    private static string NormalizeArchiveUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            !uri.Host.Equals("web.archive.org", StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }

        var normalized = url.Split('#')[0];
        const string marker = "/web/";
        var markerIndex = normalized.IndexOf(
            marker,
            StringComparison.OrdinalIgnoreCase);

        if (markerIndex < 0)
            return normalized;

        var timestampStart = markerIndex + marker.Length;
        var timestampEnd = normalized.IndexOf('/', timestampStart);

        if (timestampEnd < 0)
            return normalized;

        var timestamp = normalized[timestampStart..timestampEnd]
            .Replace("*", string.Empty, StringComparison.Ordinal);

        return normalized[..timestampStart] +
               timestamp +
               normalized[timestampEnd..];
    }
}
