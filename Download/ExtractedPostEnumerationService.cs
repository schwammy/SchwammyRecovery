namespace SchwammyRecovery.Extraction;

public interface IExtractedPostEnumerationService
{
    IReadOnlyList<string> Enumerate(string extractedDirectory);
}

public sealed class ExtractedPostEnumerationService : IExtractedPostEnumerationService
{
    private readonly Logger _logger;


    public ExtractedPostEnumerationService(Logger logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<string> Enumerate(string extractedDirectory)
    {
        if (!Directory.Exists(extractedDirectory))
        {
            _logger.Log(
                $"  SKIP: Extracted directory does not exist: {extractedDirectory}");

            return [];
        }

        return Directory
            .EnumerateDirectories(extractedDirectory)
            .Where(directory =>
                File.Exists(Path.Combine(directory, "images.json")))
            .Select(Path.GetFileName)
            .Where(slug => !string.IsNullOrWhiteSpace(slug))
            .OrderBy(slug => slug, StringComparer.OrdinalIgnoreCase)
            .ToList()!;
    }
}
