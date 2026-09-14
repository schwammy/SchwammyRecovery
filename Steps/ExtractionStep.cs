using SchwammyRecovery.Extraction;
using SchwammyRecovery.Status;

namespace SchwammyRecovery.Steps;

public sealed class ExtractionStep : IStep
{
    private readonly string _outputDirectory;
    private readonly Logger _logger;
    private readonly IRecoveredPostEnumerationService _recoveredPostEnumerationService;
    private readonly IWordPressPostExtractor _wordPressPostExtractor;
    private readonly IWordPressCommentExtractor _wordPressCommentExtractor;
    private readonly IImageExtractor _imageExtractor;
    private readonly IPostStatusStore _postStatusStore;

    public ExtractionStep(
        IRecoveredPostEnumerationService recoveredPostEnumerationService,
        IWordPressPostExtractor wordPressPostExtractor,
        IWordPressCommentExtractor wordPressCommentExtractor,
        IImageExtractor imageExtractor,
        string outputDirectory,
        Logger logger,
        IPostStatusStore postStatusStore)
    {
        _recoveredPostEnumerationService = recoveredPostEnumerationService;
        _wordPressPostExtractor = wordPressPostExtractor;
        _wordPressCommentExtractor = wordPressCommentExtractor;
        _outputDirectory = outputDirectory;
        _imageExtractor = imageExtractor;
        _logger = logger;
        _postStatusStore = postStatusStore;
    }

    public async Task RunAsync(
        CancellationToken cancellationToken = default)
    {
        var recoveredDirectory = Path.Combine(
            _outputDirectory,
            "recovered");

        var slugs = _recoveredPostEnumerationService
            .Enumerate(recoveredDirectory);

        foreach (var slug in slugs)
        {
            var existingEntry = await _postStatusStore.GetEntryAsync(slug, cancellationToken);

            if (existingEntry is not null &&
                string.Equals(existingEntry.ExtractedStatus, "S", StringComparison.OrdinalIgnoreCase))
            {
                _logger.Log($"  SKIP: Extraction already completed for {slug}.");
                continue;
            }

            try
            {
                await _wordPressPostExtractor.ExtractAsync(
                    slug,
                    cancellationToken);

                await _wordPressCommentExtractor.ExtractAsync(
                    slug,
                    cancellationToken);

                await _imageExtractor.ExtractAsync(
                    slug,
                    cancellationToken);

                await _postStatusStore.UpdateStepStatusAsync(
                    slug,
                    nameof(PostStatusEntry.ExtractedStatus),
                    "S",
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.Log($"  ERROR: Failed extracting {slug}: {ex.Message}");

                await _postStatusStore.UpdateStepStatusAsync(
                    slug,
                    nameof(PostStatusEntry.ExtractedStatus),
                    "F",
                    ex.Message,
                    cancellationToken);
            }
        }

        _logger.Log();
        _logger.Log("Extraction step complete.");
    }
}
