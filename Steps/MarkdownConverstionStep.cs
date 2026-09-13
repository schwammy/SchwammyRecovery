using System.Text.Json;
using SchwammyRecovery.Conversion;
using SchwammyRecovery.Extraction;

namespace SchwammyRecovery.Steps;

public sealed class MarkdownConversionStep
{
    private readonly IExtractedPostEnumerationService _postEnumerationService;
    private readonly IHtmlToMarkdownConverter _converter;
    private readonly string _outputDirectory;
    private readonly Logger _logger;

    public MarkdownConversionStep(
        IExtractedPostEnumerationService postEnumerationService,
        IHtmlToMarkdownConverter converter,
        string outputDirectory,
        Logger logger)
    {
        _postEnumerationService = postEnumerationService;
        _converter = converter;
        _outputDirectory = outputDirectory;
        _logger = logger;
    }

    public async Task RunAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.Log("Starting Markdown conversion...");

        var extractedDirectory = Path.Combine(
            _outputDirectory,
            "extracted");

        var slugs = _postEnumerationService.Enumerate(
            extractedDirectory);

        _logger.Log(
            $"Found {slugs.Count} extracted post(s).");

        foreach (var slug in slugs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _logger.Log(
                $"Processing Markdown: {slug}");

            await ConvertPostAsync(
                slug,
                cancellationToken);
        }

        await WriteTableOfContentsAsync(
            slugs,
            cancellationToken);

        _logger.Log("Markdown conversion complete.");
    }

    private async Task ConvertPostAsync(
        string slug,
        CancellationToken cancellationToken)
    {
        var extractedPostDirectory = Path.Combine(
            _outputDirectory,
            "extracted",
            slug);

        var contentPath = Path.Combine(
            extractedPostDirectory,
            "content.html");

        if (!File.Exists(contentPath))
        {
            _logger.Log(
                $"  SKIP: No extracted content found for {slug}.");

            return;
        }

        var imagesPath = Path.Combine(
            extractedPostDirectory,
            "images.json");

        var images = await ReadImagesAsync(
            imagesPath,
            cancellationToken);

        var markdownDirectory = Path.GetFullPath(
            Path.Combine(
                _outputDirectory,
                "markdown",
                slug));

        var markdownPath = Path.Combine(
            markdownDirectory,
            "post.md");

        if (File.Exists(markdownPath))
        {
            _logger.Log(
                $"  SKIP: Markdown already exists for {slug}; rerun only when the file is missing.");

            return;
        }

        var html = await File.ReadAllTextAsync(
            contentPath,
            cancellationToken);

        var imagesDirectory = Path.GetFullPath(
            Path.Combine(
                _outputDirectory,
                "images",
                slug));

        var imagePathPrefix = Path.GetRelativePath(
            markdownDirectory,
            imagesDirectory);

        var markdown = _converter.Convert(
            html,
            images,
            imagePathPrefix,
            imagesDirectory);

        Directory.CreateDirectory(
            markdownDirectory);

        await File.WriteAllTextAsync(
            markdownPath,
            markdown,
            cancellationToken);

        _logger.Log(
            $"  Created: {markdownPath}");
    }

    private async Task WriteTableOfContentsAsync(
        IReadOnlyList<string> slugs,
        CancellationToken cancellationToken)
    {
        var markdownDirectory = Path.Combine(
            _outputDirectory,
            "markdown");

        var tocPath = Path.Combine(
            markdownDirectory,
            "index.md");

        Directory.CreateDirectory(markdownDirectory);

        var lines = new List<string>
        {
            "# Table of Contents",
            string.Empty,
            "- [Overview](../README.md)",
            string.Empty
        };

        foreach (var slug in slugs)
        {
            var markdownPath = Path.Combine(
                markdownDirectory,
                slug,
                "post.md");

            if (!File.Exists(markdownPath))
            {
                continue;
            }

            lines.Add(
                $"- [{slug}]({slug}/post.md)");
        }

        if (lines.Count == 3)
        {
            lines.Add("No posts have been converted to Markdown yet.");
        }

        var content = string.Join(
            Environment.NewLine,
            lines);

        await File.WriteAllTextAsync(
            tocPath,
            content + Environment.NewLine,
            cancellationToken);

        _logger.Log(
            $"  Created: {tocPath}");
    }

    private static async Task<IReadOnlyList<RecoveredImage>> ReadImagesAsync(
        string imagesPath,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(imagesPath))
            return [];

        var json = await File.ReadAllTextAsync(
            imagesPath,
            cancellationToken);

        return JsonSerializer.Deserialize<List<RecoveredImage>>(
                   json) ??
               [];
    }
}
