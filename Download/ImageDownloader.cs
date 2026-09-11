using System.Text.Json;

namespace SchwammyRecovery.Extraction;

public interface IImageDownloader
{
    Task DownloadAsync(
    string slug,
    CancellationToken cancellationToken = default);
}

public sealed class ImageDownloader : IImageDownloader
{
    private readonly string _outputDirectory;
    private readonly HttpClient _httpClient;
    private readonly Logger _logger;


    public ImageDownloader(
        string outputDirectory,
        HttpClient httpClient,
        Logger logger)
    {
        _outputDirectory = outputDirectory;
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task DownloadAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        var imagesPath = Path.Combine(
            _outputDirectory,
            "extracted",
            slug,
            "images.json");

        if (!File.Exists(imagesPath))
        {
            _logger.Log(
                $"  SKIP: No extracted images found for {slug}.");
            return;
        }

        var imagesDirectory = Path.Combine(
            _outputDirectory,
            "images",
            slug);

        Directory.CreateDirectory(imagesDirectory);

        var json = await File.ReadAllTextAsync(
            imagesPath,
            cancellationToken);

        var images = JsonSerializer.Deserialize<List<RecoveredImage>>(
            json) ?? [];

        foreach (var image in images)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await DownloadImageAsync(
                image,
                imagesDirectory,
                cancellationToken);
        }
    }

    private async Task DownloadImageAsync(
        RecoveredImage image,
        string imagesDirectory,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(image.FileName))
        {
            _logger.Log(
                "  SKIP: Image has no usable filename.");
            return;
        }

        if (!string.IsNullOrWhiteSpace(image.LinkedImageUrl) &&
            !string.IsNullOrWhiteSpace(image.LinkedImageFileName))
        {
            var linkedImagePath = Path.Combine(
                imagesDirectory,
                image.LinkedImageFileName);

            if (File.Exists(linkedImagePath))
            {
                _logger.Log(
                    $"  SKIP: {image.LinkedImageFileName} already exists.");
            }
            else if (await TryDownloadAsync(
                         image.LinkedImageUrl,
                         linkedImagePath,
                         cancellationToken))
            {
                _logger.Log(
                    $"  Downloaded linked image: {image.LinkedImageFileName}");
                return;
            }
        }

        var sourcePath = Path.Combine(
            imagesDirectory,
            image.FileName);

        if (File.Exists(sourcePath))
        {
            _logger.Log(
                $"  SKIP: {image.FileName} already exists.");
            return;
        }

        if (await TryDownloadAsync(
                image.SourceUrl,
                sourcePath,
                cancellationToken))
        {
            _logger.Log(
                $"  Downloaded: {image.FileName}");
            return;
        }

        _logger.Log(
            $"  FAILED: Could not download {image.FileName}.");
    }

    private async Task<bool> TryDownloadAsync(
        string url,
        string outputPath,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(
                url,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
                return false;

            var contentType =
                response.Content.Headers.ContentType?.MediaType;

            if (!IsImageContentType(contentType))
                return false;

            await using var input =
                await response.Content.ReadAsStreamAsync(
                    cancellationToken);

            await using var output = File.Create(outputPath);

            await input.CopyToAsync(
                output,
                cancellationToken);

            return true;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    private static bool IsImageContentType(string? contentType)
    {
        return !string.IsNullOrWhiteSpace(contentType) &&
               contentType.StartsWith(
                   "image/",
                   StringComparison.OrdinalIgnoreCase);
    }


}
