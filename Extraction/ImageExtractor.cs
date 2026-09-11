using System.Text.Json;
using HtmlAgilityPack;

namespace SchwammyRecovery.Extraction;

public interface IImageExtractor
{
    Task ExtractAsync(
    string slug,
    CancellationToken cancellationToken = default);
}

public sealed class ImageExtractor : IImageExtractor
{
    private readonly string _outputDirectory;
    private readonly Logger _logger;

    public ImageExtractor(
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
        var contentPath = Path.Combine(
            _outputDirectory,
            "extracted",
            slug,
            "content.html");

        if (!File.Exists(contentPath))
        {
            _logger.Log(
                $"  SKIP: No extracted content found for {slug}.");
            return;
        }

        var imagesPath = Path.Combine(
            _outputDirectory,
            "extracted",
            slug,
            "images.json");

        if (File.Exists(imagesPath))
        {
            _logger.Log(
                "  SKIP: Images already extracted.");
            return;
        }

        var html = await File.ReadAllTextAsync(
            contentPath,
            cancellationToken);

        var document = new HtmlDocument();
        document.LoadHtml(html);

        var imageNodes = document.DocumentNode.SelectNodes("//img");

        var images = new List<RecoveredImage>();

        if (imageNodes is not null)
        {
            foreach (var imageNode in imageNodes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var sourceUrl = imageNode.GetAttributeValue(
                    "src",
                    string.Empty);

                if (string.IsNullOrWhiteSpace(sourceUrl))
                    continue;

                var linkUrl = imageNode.ParentNode?.Name == "a"
                    ? imageNode.ParentNode.GetAttributeValue(
                        "href",
                        string.Empty)
                    : null;

                if (string.IsNullOrWhiteSpace(linkUrl))
                    linkUrl = null;

                var linkedImageUrl = IsImageUrl(linkUrl)
                    ? linkUrl
                    : null;

                images.Add(new RecoveredImage
                {
                    SourceUrl = sourceUrl,
                    LinkUrl = linkUrl,
                    FileName = GetFileName(sourceUrl),
                    LinkedImageUrl = linkedImageUrl,
                    LinkedImageFileName = GetFileName(linkedImageUrl)
                });
            }
        }

        var json = JsonSerializer.Serialize(
            images,
            new JsonSerializerOptions
            {
                WriteIndented = true
            });

        await File.WriteAllTextAsync(
            imagesPath,
            json,
            cancellationToken);

        _logger.Log(
            $"  Found {images.Count} image(s).");
        _logger.Log(
            $"  Images: {imagesPath}");
    }

    private static bool IsImageUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        if (!Uri.TryCreate(
                url,
                UriKind.Absolute,
                out var uri))
        {
            return false;
        }

        var path = uri.AbsolutePath;

        return path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".webp", StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetFileName(string? sourceUrl)
    {
        if (string.IsNullOrWhiteSpace(sourceUrl))
            return null;

        if (!Uri.TryCreate(
                sourceUrl,
                UriKind.Absolute,
                out var uri))
        {
            return null;
        }

        var path = Uri.UnescapeDataString(
            uri.AbsolutePath);

        var fileName = Path.GetFileName(path);

        return string.IsNullOrWhiteSpace(fileName)
            ? null
            : fileName;
    }


}

public sealed class RecoveredImage
{
    public string SourceUrl { get; init; } = string.Empty;


    public string? LinkUrl { get; init; }

    public string? FileName { get; init; }

    public string? LinkedImageUrl { get; init; }

    public string? LinkedImageFileName { get; init; }


}
