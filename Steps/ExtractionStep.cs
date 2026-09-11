using SchwammyRecovery.Extraction;

namespace SchwammyRecovery.Steps;

public sealed class ExtractionStep : IStep
{
    private readonly string _outputDirectory;
    private readonly Logger _logger;
    private readonly IRecoveredPostEnumerationService _recoveredPostEnumerationService;
    private readonly IWordPressPostExtractor _wordPressPostExtractor;
    private readonly IWordPressCommentExtractor _wordPressCommentExtractor;
    private readonly IImageExtractor _imageExtractor;

    public ExtractionStep(
        IRecoveredPostEnumerationService recoveredPostEnumerationService,
        IWordPressPostExtractor wordPressPostExtractor,
        IWordPressCommentExtractor wordPressCommentExtractor,
        IImageExtractor imageExtractor,
        string outputDirectory,
        Logger logger)
    {
        _recoveredPostEnumerationService = recoveredPostEnumerationService;
        _wordPressPostExtractor = wordPressPostExtractor;
        _wordPressCommentExtractor = wordPressCommentExtractor;
        _outputDirectory = outputDirectory;
        _imageExtractor = imageExtractor;
        _logger = logger;
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
            await _wordPressPostExtractor.ExtractAsync(
                slug,
                cancellationToken);

            await _wordPressCommentExtractor.ExtractAsync(
                slug,
                cancellationToken);

            await _imageExtractor.ExtractAsync(
slug,
cancellationToken);
        }

        _logger.Log();
        _logger.Log("Extraction step complete.");
    }
}
