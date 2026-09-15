using System.Net;
using System.Text.Json;

using HtmlAgilityPack;

namespace SchwammyRecovery.Extraction;

public interface IWordPressCommentExtractor
{
    Task ExtractAsync(
        string slug,
        CancellationToken cancellationToken = default);
}

public sealed class WordPressCommentExtractor
    : IWordPressCommentExtractor
{
    private readonly string _outputDirectory;
    private readonly Logger _logger;

    public WordPressCommentExtractor(
        string outputDirectory,
        Logger logger)
    {
        _outputDirectory = outputDirectory;
        _logger = logger;
    }

    public async Task ExtractAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        var recoveredDirectory = Path.Combine(
            _outputDirectory,
            "recovered",
            slug);

        var sourcePath = Path.Combine(
            recoveredDirectory,
            "source.html");

        if (!File.Exists(sourcePath))
        {
            _logger.Log(
                $"Skipping {slug}: recovered source not found.");

            return;
        }

        var extractedDirectory = Path.Combine(
            _outputDirectory,
            "extracted",
            slug);

        var commentsPath = Path.Combine(
            extractedDirectory,
            "comments.json");

        var html = await File.ReadAllTextAsync(
            sourcePath,
            cancellationToken);

        var document = new HtmlDocument();
        document.LoadHtml(html);

        var comments = ExtractComments(
            document.DocumentNode);

        Directory.CreateDirectory(extractedDirectory);

        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        await File.WriteAllTextAsync(
            commentsPath,
            JsonSerializer.Serialize(comments, jsonOptions),
            cancellationToken);

        _logger.Log(
            $"Extracted comments for {slug}: " +
            $"comments={comments.Count}.");
    }

    private static List<RecoveredComment> ExtractComments(
        HtmlNode document)
    {
        var comments = new List<RecoveredComment>();

        var commentNodes = document.SelectNodes(
            "//div[@id='comments']" +
            "//ol[contains(@class, 'commentlist')]" +
            "/li");

        if (commentNodes is null)
            return comments;

        foreach (var node in commentNodes)
        {
            var classes = node
                .GetAttributeValue("class", "")
                .Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries);

            var type =
                classes.Contains("pingback")
                    ? "pingback"
                    : classes.Contains("trackback")
                        ? "trackback"
                        : "comment";

            if (type is "pingback" or "trackback")
            {
                var link = node.SelectSingleNode(
                    ".//a[contains(@class, 'url')]");

                comments.Add(new RecoveredComment
                {
                    Id = node.GetAttributeValue("id", null),
                    Type = type,
                    Content = DecodeText(link?.InnerText),
                    Url = NormalizePublishedUrl(link?.GetAttributeValue(
                        "href",
                        null))
                });

                continue;
            }

            comments.Add(
                ExtractRegularComment(node));
        }

        return comments;
    }

    private static string? ExtractParentId(
        HtmlNode node)
    {
        var classes = node
            .GetAttributeValue("class", "")
            .Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries);

        var parentClass = classes.FirstOrDefault(
            x => x.StartsWith("parent-"));

        return parentClass?
            .Substring("parent-".Length);
    }

    private static RecoveredComment ExtractRegularComment(
        HtmlNode node)
    {
        var authorNode = node.SelectSingleNode(
            ".//*[contains(concat(' ', normalize-space(@class), ' '), ' fn ')]");

        var authorLink = authorNode?
            .SelectSingleNode(
                ".//a[contains(concat(' ', normalize-space(@class), ' '), ' url ')]");

        var author = DecodeText(authorNode?.InnerText);

        var dateNode = node.SelectSingleNode(
            ".//time");

        var contentNode = node.SelectSingleNode(
            ".//*[contains(concat(' ', normalize-space(@class), ' '), ' comment-content ')]");

        contentNode?
            .SelectSingleNode(
                ".//a[contains(concat(' ', normalize-space(@class), ' '), ' comment-reply-link ')]")
            ?.Remove();

        return new RecoveredComment
        {
            Id = node.GetAttributeValue("id", null),
            Type = "comment",
            Author = author,
            AuthorUrl = NormalizePublishedUrl(authorLink?
                .GetAttributeValue("href", null)),
            Date = dateNode?
                .GetAttributeValue("datetime", null)
                ?? DecodeText(dateNode?.InnerText),
            Content = DecodeText(contentNode?.InnerText),
            Url = NormalizePublishedUrl(dateNode?
                .ParentNode?
                .GetAttributeValue("href", null)),
            ParentId = ExtractParentId(node)
        };
    }

    private static string? DecodeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        var decoded = value.Trim();

        for (var index = 0; index < 3; index++)
        {
            var next = WebUtility.HtmlDecode(decoded);
            if (string.Equals(next, decoded, StringComparison.Ordinal))
                break;

            decoded = next;
        }

        return decoded;
    }

    private static string? NormalizePublishedUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        var url = WebUtility.HtmlDecode(value.Trim());

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            !uri.Host.Equals("web.archive.org", StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }

        const string marker = "/web/";
        var markerIndex = url.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
            return url;

        var remainder = url[(markerIndex + marker.Length)..];
        var separatorIndex = remainder.IndexOf('/');
        if (separatorIndex < 0)
            return url;

        var originalUrl = remainder[(separatorIndex + 1)..];
        return originalUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
               originalUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? Uri.UnescapeDataString(originalUrl)
            : url;
    }
}
