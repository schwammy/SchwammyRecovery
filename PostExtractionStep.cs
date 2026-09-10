using System.Text.Json;
using HtmlAgilityPack;

namespace SchwammyRecovery;

public sealed class PostExtractionStep
{
    private readonly string _outputDirectory;
    private readonly Logger _logger;

    public PostExtractionStep(
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

        var capturePath = Path.Combine(
            recoveredDirectory,
            "capture.json");

        if (!File.Exists(sourcePath) ||
            !File.Exists(capturePath))
        {
            _logger.Log(
                $"Skipping {slug}: recovered source not found.");

            return;
        }

        var extractedDirectory = Path.Combine(
            _outputDirectory,
            "extracted",
            slug);

        var postPath = Path.Combine(
            extractedDirectory,
            "post.json");

        var contentPath = Path.Combine(
            extractedDirectory,
            "content.html");

        var commentsPath = Path.Combine(
            extractedDirectory,
            "comments.json");

        if (File.Exists(postPath) &&
            File.Exists(contentPath) &&
            File.Exists(commentsPath))
        {
            _logger.Log(
                $"Skipping {slug}: already extracted.");

            return;
        }

        var html = await File.ReadAllTextAsync(
            sourcePath,
            cancellationToken);

        var captureJson = await File.ReadAllTextAsync(
            capturePath,
            cancellationToken);

        var capture = JsonSerializer.Deserialize<WaybackCapture>(
            captureJson);

        if (capture is null)
        {
            _logger.Log(
                $"Skipping {slug}: invalid capture metadata.");

            return;
        }

        var document = new HtmlDocument();
        document.LoadHtml(html);

        var article = document.DocumentNode.SelectSingleNode(
            "//article");

        if (article is null)
        {
            _logger.Log(
                $"Skipping {slug}: article not found.");

            return;
        }

        var title = ExtractTitle(article);
        var published = ExtractPublished(article);
        var author = ExtractAuthor(article);
        var categories = ExtractCategories(article);

        var contentNode = article.SelectSingleNode(
            ".//div[contains(concat(' ', normalize-space(@class), ' '), ' entry-content ')]");

        if (contentNode is null)
        {
            _logger.Log(
                $"Skipping {slug}: entry-content not found.");

            return;
        }

        var contentHtml = GetInnerHtml(contentNode);

        var comments = ExtractComments(
            document.DocumentNode);

        Directory.CreateDirectory(extractedDirectory);

        var metadata = new PostMetadata
        {
            Title = title ?? slug,
            Slug = slug,
            Published = published,
            Author = author,
            Categories = categories,
            SourceUrl = capture.OriginalUrl,
            Source = capture
        };

        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        await File.WriteAllTextAsync(
            postPath,
            JsonSerializer.Serialize(metadata, jsonOptions),
            cancellationToken);

        await File.WriteAllTextAsync(
            contentPath,
            contentHtml,
            cancellationToken);

        await File.WriteAllTextAsync(
            commentsPath,
            JsonSerializer.Serialize(comments, jsonOptions),
            cancellationToken);

        _logger.Log(
            $"Extracted {slug}: " +
            $"title=\"{metadata.Title}\", " +
            $"comments={comments.Count}.");
    }

    private static string? ExtractTitle(
        HtmlNode article)
    {
        return article
            .SelectSingleNode(".//h1[contains(@class, 'entry-title')]")
            ?.InnerText
            .Trim();
    }

    private static string? ExtractPublished(
        HtmlNode article)
    {
        return article
            .SelectSingleNode(
                ".//time[contains(@class, 'entry-date')]")
            ?.GetAttributeValue("datetime", null);
    }

    private static string? ExtractAuthor(
     HtmlNode article)
    {
        return article
            .SelectSingleNode(
                ".//span[contains(@class, 'author')]" +
                "//a[contains(@class, 'fn')]")
            ?.InnerText
            .Trim();
    }

    private static List<string> ExtractCategories(
        HtmlNode article)
    {
        var categoryLinks = article.SelectNodes(
            ".//footer[contains(@class, 'entry-footer')]"
            + "//a[contains(@rel, 'category') or contains(@class, 'category')]");

        if (categoryLinks is null)
            return [];

        return categoryLinks
            .Select(x => x.InnerText.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();
    }

    private static string GetInnerHtml(
        HtmlNode node)
    {
        return string.Concat(
            node.ChildNodes.Select(
                child => child.OuterHtml));
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
                    Content = link?.InnerText.Trim(),
                    Url = link?.GetAttributeValue(
                        "href",
                        null)
                });

                continue;
            }

            comments.Add(ExtractRegularComment(node));
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

        var author = authorNode?.InnerText.Trim();

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
            AuthorUrl = authorLink?
                .GetAttributeValue("href", null),
            Date = dateNode?
                .GetAttributeValue("datetime", null)
                ?? dateNode?.InnerText.Trim(),
            Content = contentNode?.InnerText.Trim(),
            Url = dateNode?
                .ParentNode?
                .GetAttributeValue("href", null),
            ParentId = ExtractParentId(node)
        };
    }
}