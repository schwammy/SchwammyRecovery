using System.Net;
using System.Text.Json;
using SchwammyRecovery.Extraction;
using SchwammyRecovery.Steps;

namespace SchwammyRecovery.Export;

public sealed class PortableMarkdownExportStep : IStep
{
    private readonly string _outputDirectory;
    private readonly Logger _logger;

    public PortableMarkdownExportStep(
        string outputDirectory,
        Logger logger)
    {
        _outputDirectory = outputDirectory;
        _logger = logger;
    }

    public async Task RunAsync(
        CancellationToken cancellationToken = default)
    {
        var extractedDirectory = Path.Combine(_outputDirectory, "extracted");
        var markdownDirectory = Path.Combine(_outputDirectory, "markdown");
        var exportDirectory = Path.Combine(_outputDirectory, "export", "portable-markdown");
        var exportPostsDirectory = Path.Combine(exportDirectory, "posts");
        var exportAssetsDirectory = Path.Combine(exportDirectory, "assets");
        var exportCommentsDirectory = Path.Combine(exportDirectory, "comments");
        var exportedSlugs = new List<string>();

        if (!Directory.Exists(extractedDirectory))
        {
            _logger.Log("No extracted posts found; portable export skipped.");
            return;
        }

        foreach (var extractedPostDirectory in Directory.EnumerateDirectories(extractedDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var slug = Path.GetFileName(extractedPostDirectory);
            var metadataPath = Path.Combine(extractedPostDirectory, "post.json");
            var markdownPath = Path.Combine(markdownDirectory, slug, "post.md");

            if (!File.Exists(metadataPath) || !File.Exists(markdownPath))
            {
                _logger.Log($"  SKIP: Export inputs incomplete for {slug}.");
                continue;
            }

            var metadata = await ReadMetadataAsync(metadataPath, cancellationToken);
            if (metadata is null)
            {
                _logger.Log($"  SKIP: Invalid post metadata for {slug}.");
                continue;
            }

            var body = await File.ReadAllTextAsync(markdownPath, cancellationToken);
            body = body.Replace(
                $"../../images/{slug}/",
                $"../assets/{slug}/",
                StringComparison.Ordinal);

            var postDirectory = Path.Combine(exportPostsDirectory, slug);
            Directory.CreateDirectory(postDirectory);

            var exportPostPath = Path.Combine(postDirectory, "post.md");
            await File.WriteAllTextAsync(
                exportPostPath,
                BuildMarkdown(metadata, body),
                cancellationToken);

            var sourceImagesDirectory = Path.Combine(_outputDirectory, "images", slug);
            if (Directory.Exists(sourceImagesDirectory))
            {
                CopyDirectory(
                    sourceImagesDirectory,
                    Path.Combine(exportAssetsDirectory, slug));
            }

            var commentsPath = Path.Combine(extractedPostDirectory, "comments.json");
            if (File.Exists(commentsPath))
            {
                Directory.CreateDirectory(exportCommentsDirectory);
                File.Copy(
                    commentsPath,
                    Path.Combine(exportCommentsDirectory, $"{slug}.json"),
                    overwrite: true);
            }

            exportedSlugs.Add(slug);
            _logger.Log($"  Exported: {exportPostPath}");
        }

        Directory.CreateDirectory(exportDirectory);
        var manifest = new
        {
            Format = "portable-markdown",
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Posts = exportedSlugs
        };

        await File.WriteAllTextAsync(
            Path.Combine(exportDirectory, "manifest.json"),
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);

        _logger.Log($"Portable Markdown export complete: {exportedSlugs.Count} post(s).");
    }

    private static async Task<PostMetadata?> ReadMetadataAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var json = await File.ReadAllTextAsync(path, cancellationToken);
        return JsonSerializer.Deserialize<PostMetadata>(json);
    }

    private static string BuildMarkdown(PostMetadata metadata, string body)
    {
        var lines = new List<string>
        {
            "---",
            $"title: {YamlString(metadata.Title)}",
            $"slug: {YamlString(metadata.Slug)}"
        };

        if (!string.IsNullOrWhiteSpace(metadata.Published))
            lines.Add($"date: {YamlString(metadata.Published)}");

        if (!string.IsNullOrWhiteSpace(metadata.Author))
            lines.Add($"author: {YamlString(metadata.Author)}");

        if (metadata.Categories.Count > 0)
        {
            lines.Add(
                $"categories: [{string.Join(", ", metadata.Categories.Select(YamlString))}]");
        }

        lines.Add("---");
        lines.Add(string.Empty);
        lines.Add(body.Trim());

        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    private static string YamlString(string value)
    {
        return JsonSerializer.Serialize(WebUtility.HtmlDecode(value));
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (var file in Directory.EnumerateFiles(sourceDirectory))
        {
            File.Copy(
                file,
                Path.Combine(destinationDirectory, Path.GetFileName(file)),
                overwrite: true);
        }
    }
}