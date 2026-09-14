namespace SchwammyRecovery.Steps;

using SchwammyRecovery.Extraction;
using SchwammyRecovery.Status;

public sealed class ImageDownloadStep : IStep
{
    private readonly IExtractedPostEnumerationService _postEnumerationService;
    private readonly IImageDownloader _imageDownloader;
    private readonly string _extractedDirectory;
    private readonly Logger _logger;
    private readonly IPostStatusStore _postStatusStore;


    public ImageDownloadStep(
        IExtractedPostEnumerationService postEnumerationService,
        IImageDownloader imageDownloader,
        string extractedDirectory,
        Logger logger,
        IPostStatusStore postStatusStore)
    {
        _postEnumerationService = postEnumerationService;
        _imageDownloader = imageDownloader;
        _extractedDirectory = extractedDirectory;
        _logger = logger;
        _postStatusStore = postStatusStore;
    }

    public async Task RunAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.Log("Starting image download...");

        var slugs = _postEnumerationService.Enumerate(
            _extractedDirectory);

        _logger.Log(
            $"Found {slugs.Count} extracted post(s) with images.");

        foreach (var slug in slugs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var existingEntry = await _postStatusStore.GetEntryAsync(slug, cancellationToken);

            if (existingEntry is not null &&
                string.Equals(existingEntry.ImagesStatus, "S", StringComparison.OrdinalIgnoreCase))
            {
                _logger.Log($"  SKIP: Image download already completed for {slug}.");
                continue;
            }

            _logger.Log($"Processing images: {slug}");

            try
            {
                await _imageDownloader.DownloadAsync(
                    slug,
                    cancellationToken);

                await _postStatusStore.UpdateStepStatusAsync(
                    slug,
                    nameof(PostStatusEntry.ImagesStatus),
                    "S",
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.Log($"  ERROR: Failed downloading images for {slug}: {ex.Message}");

                await _postStatusStore.UpdateStepStatusAsync(
                    slug,
                    nameof(PostStatusEntry.ImagesStatus),
                    "F",
                    ex.Message,
                    cancellationToken);
            }
        }

        _logger.Log("Image download complete.");
    }
}
