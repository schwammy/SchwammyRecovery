namespace SchwammyRecovery.Steps;

using SchwammyRecovery.Extraction;

public sealed class ImageDownloadStep
{
    private readonly IExtractedPostEnumerationService _postEnumerationService;
    private readonly IImageDownloader _imageDownloader;
    private readonly string _extractedDirectory;
    private readonly Logger _logger;


    public ImageDownloadStep(
        IExtractedPostEnumerationService postEnumerationService,
        IImageDownloader imageDownloader,
        string extractedDirectory,
        Logger logger)
    {
        _postEnumerationService = postEnumerationService;
        _imageDownloader = imageDownloader;
        _extractedDirectory = extractedDirectory;
        _logger = logger;
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

            _logger.Log($"Processing images: {slug}");

            await _imageDownloader.DownloadAsync(
                slug,
                cancellationToken);
        }

        _logger.Log("Image download complete.");
    }
}
