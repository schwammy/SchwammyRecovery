using System.Text.Json;
using HtmlAgilityPack;

namespace SchwammyRecovery;

public sealed class ArchiveCrawler
{
    private readonly WaybackClient _client;
    private readonly string _output;
    private readonly HashSet<string> _visitedArchivePages = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _postUrls = new(StringComparer.OrdinalIgnoreCase);

    public ArchiveCrawler(WaybackClient client, string output)
    {
        _client = client;
        _output = output;
        Directory.CreateDirectory(_output);
    }

    public async Task CrawlArchiveAsync(string startUrl)
    {
        Console.WriteLine($"Starting at:\n  {startUrl}\n");

        // Phase 1: crawl the supplied monthly archive and its pagination.
        await CrawlArchivePageAsync(startUrl);

        var posts = _postUrls
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        await File.WriteAllTextAsync(
            Path.Combine(_output, "post-urls.json"),
            JsonSerializer.Serialize(posts, new JsonSerializerOptions { WriteIndented = true }));

        Console.WriteLine($"\nDiscovered {posts.Length} unique post URLs.");
    }

    private async Task CrawlArchivePageAsync(string archiveUrl)
    {
        if (!_visitedArchivePages.Add(archiveUrl))
            return;

        Console.WriteLine();
        Console.WriteLine($"==================================================");
        Console.WriteLine($"Archive: {archiveUrl}");
        Console.WriteLine($"==================================================");

        var html = await _client.GetHtmlAsync(archiveUrl);

        if (html is null)
        {
            Console.WriteLine($"  ERROR: Could not retrieve archive page.");
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
                    Console.WriteLine(
                        $"  Following pagination: {absolute}");

                    await CrawlArchivePageAsync(absolute);
                }

                continue;
            }

            // Is this a link back to Schwammy Says?
            if (!IsOriginalSchwammyUrl(original))
                continue;

            if (!LooksLikePostUrl(original))
                continue;

            if (_postUrls.Add(original))
            {
                Console.WriteLine(
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
}
