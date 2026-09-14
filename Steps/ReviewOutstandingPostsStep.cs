namespace SchwammyRecovery.Steps;

public sealed class ReviewOutstandingPostsStep : IStep
{
    private readonly string _outputDirectory;
    private readonly Logger _logger;
    private readonly IOutstandingPostsReviewService _reviewService;

    public ReviewOutstandingPostsStep(
        IOutstandingPostsReviewService reviewService,
        string outputDirectory,
        Logger logger)
    {
        _reviewService = reviewService;
        _outputDirectory = outputDirectory;
        _logger = logger;
    }

    public async Task RunAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.Log();
        _logger.Log("Reviewing outstanding posts...");

        try
        {
            var result = await _reviewService.ReviewAsync(
                _outputDirectory,
                cancellationToken);

            _logger.Log($"  Discovered: {result.DiscoveredCount}");
            _logger.Log($"  Recovered: {result.RecoveredCount}");
            _logger.Log($"  Outstanding: {result.OutstandingPosts.Count}");

            if (result.OutstandingPosts.Count == 0)
            {
                _logger.Log("  All discovered posts appear to be recovered.");
            }
            else
            {
                _logger.Log("  Outstanding posts:");

                foreach (var outstandingPost in result.OutstandingPosts)
                {
                    _logger.Log($"    - {outstandingPost}");
                }
            }

            _logger.Log($"  Posts with missing image downloads: {result.MissingImagePosts.Count}");

            if (result.MissingImagePosts.Count == 0)
            {
                _logger.Log("  No posts are missing downloaded images.");
            }
            else
            {
                _logger.Log("  Posts with missing image downloads:");

                foreach (var (slug, missingImages) in result.MissingImagePosts)
                {
                    if (missingImages.LocalImages.Count > 0)
                    {
                        _logger.Log($"    - {slug} (local blog images): {string.Join(", ", missingImages.LocalImages)}");
                    }

                    if (missingImages.ExternalImages.Count > 0)
                    {
                        _logger.Log($"    - {slug} (external images): {string.Join(", ", missingImages.ExternalImages)}");
                    }
                }
            }

            if (result.MissingImageFiles.Count == 0)
            {
                _logger.Log("  No missing image files were found.");
            }
            else
            {
                _logger.Log("  Missing image files:");

                foreach (var missingImageFile in result.MissingImageFiles)
                {
                    _logger.Log($"    - {missingImageFile}");
                }
            }

            if (result.MissingLocalImageFiles.Count == 0)
            {
                _logger.Log("  No missing local blog-hosted image files were found.");
            }
            else
            {
                _logger.Log("  Missing local blog-hosted image files:");

                foreach (var missingLocalImageFile in result.MissingLocalImageFiles)
                {
                    _logger.Log($"    - {missingLocalImageFile}");
                }
            }

            if (result.MissingExternalImageFiles.Count == 0)
            {
                _logger.Log("  No missing external image files were found.");
            }
            else
            {
                _logger.Log("  Missing external image files:");

                foreach (var missingExternalImageFile in result.MissingExternalImageFiles)
                {
                    _logger.Log($"    - {missingExternalImageFile}");
                }
            }
        }
        catch (FileNotFoundException ex)
        {
            _logger.Log($"  ERROR: {ex.Message}");
        }
    }
}
