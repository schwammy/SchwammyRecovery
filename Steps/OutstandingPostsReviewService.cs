using SchwammyRecovery.Extraction;
using System.Text.Json;

namespace SchwammyRecovery.Steps;

public sealed class OutstandingPostsReviewService : IOutstandingPostsReviewService
{
    private readonly PostUrlReader _postUrlReader;

    public OutstandingPostsReviewService(
        PostUrlReader postUrlReader)
    {
        _postUrlReader = postUrlReader;
    }

    public async Task<OutstandingPostsReviewResult> ReviewAsync(
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        var postUrlPath = Path.Combine(
            outputDirectory,
            "discovery",
            "post-urls.json");

        if (!File.Exists(postUrlPath))
            throw new FileNotFoundException(
                $"{postUrlPath} was not found.",
                postUrlPath);

        var discoveredPosts = await _postUrlReader.ReadAsync(postUrlPath);

        var recoveredDirectory = Path.Combine(
            outputDirectory,
            "recovered");

        var recoveredSlugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (Directory.Exists(recoveredDirectory))
        {
            foreach (var postDirectory in Directory.EnumerateDirectories(recoveredDirectory))
            {
                var slug = Path.GetFileName(postDirectory);

                if (string.IsNullOrWhiteSpace(slug))
                    continue;

                var sourcePath = Path.Combine(postDirectory, "source.html");
                var capturePath = Path.Combine(postDirectory, "capture.json");

                if (File.Exists(sourcePath) && File.Exists(capturePath))
                {
                    recoveredSlugs.Add(slug);
                }
            }
        }

        var outstandingPosts = discoveredPosts
            .Where(postUrl => !recoveredSlugs.Contains(GetSlug(postUrl)))
            .ToList();

        var missingImagePosts = new Dictionary<string, MissingImageReport>(StringComparer.OrdinalIgnoreCase);
        var missingImageFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var missingLocalImageFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var missingExternalImageFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var postUrl in discoveredPosts)
        {
            var slug = GetSlug(postUrl);

            if (string.IsNullOrWhiteSpace(slug))
                continue;

            var imagesJsonPath = Path.Combine(
                outputDirectory,
                "extracted",
                slug,
                "images.json");

            if (!File.Exists(imagesJsonPath))
                continue;

            var imagesJson = await File.ReadAllTextAsync(
                imagesJsonPath,
                cancellationToken);

            var images = JsonSerializer.Deserialize<List<RecoveredImage>>(imagesJson) ?? [];

            var imagesDirectory = Path.Combine(
                outputDirectory,
                "images",
                slug);

            var missingLocalImages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var missingExternalImages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var image in images)
            {
                var imageAttempts = new List<(string FileName, string? Url)>();

                if (!string.IsNullOrWhiteSpace(image.LinkedImageUrl) &&
                    !string.IsNullOrWhiteSpace(image.LinkedImageFileName))
                {
                    imageAttempts.Add((image.LinkedImageFileName, image.LinkedImageUrl));
                }

                if (!string.IsNullOrWhiteSpace(image.SourceUrl) &&
                    !string.IsNullOrWhiteSpace(image.FileName))
                {
                    imageAttempts.Add((image.FileName, image.SourceUrl));
                }

                if (imageAttempts.Count == 0)
                    continue;

                var anyDownloaded = imageAttempts
                    .Any(attempt => File.Exists(
                        Path.Combine(imagesDirectory, attempt.FileName)));

                if (anyDownloaded)
                    continue;

                foreach (var attempt in imageAttempts
                             .DistinctBy(attempt => attempt.FileName, StringComparer.OrdinalIgnoreCase))
                {
                    missingImageFiles.Add(attempt.FileName);

                    if (IsLocalBlogImageUrl(attempt.Url))
                    {
                        missingLocalImages.Add(attempt.FileName);
                    }
                    else
                    {
                        missingExternalImages.Add(attempt.FileName);
                    }
                }
            }

            if (missingLocalImages.Count > 0 || missingExternalImages.Count > 0)
            {
                missingImagePosts[slug] = new MissingImageReport
                {
                    LocalImages = missingLocalImages
                        .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
                        .ToList(),
                    ExternalImages = missingExternalImages
                        .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
                        .ToList()
                };
            }
        }

        return new OutstandingPostsReviewResult
        {
            DiscoveredCount = discoveredPosts.Count,
            RecoveredCount = recoveredSlugs.Count,
            OutstandingPosts = outstandingPosts,
            MissingImagePosts = missingImagePosts,
            MissingImageFiles = missingImageFiles
                .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            MissingLocalImageFiles = missingLocalImageFiles
                .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            MissingExternalImageFiles = missingExternalImageFiles
                .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    private static bool IsLocalBlogImageUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        try
        {
            var uri = new Uri(url);
            var host = uri.Host;

            return host.Equals("schwammysays.net", StringComparison.OrdinalIgnoreCase) ||
                   host.Equals("www.schwammysays.net", StringComparison.OrdinalIgnoreCase) ||
                   host.EndsWith(".schwammysays.net", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
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
