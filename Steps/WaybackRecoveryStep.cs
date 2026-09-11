using System.Text.Json;

namespace SchwammyRecovery.Steps;

public sealed class WaybackRecoveryStep : IStep
{
    private readonly PostUrlReader _postUrlReader;
    private readonly WaybackClient _wayback;
    private readonly string _output;
    private readonly Logger _logger;

    public WaybackRecoveryStep(
        PostUrlReader postUrlReader,
        WaybackClient wayback,
        string output,
        Logger logger)
    {
        _postUrlReader = postUrlReader;
        _wayback = wayback;
        _output = output;
        _logger = logger;
    }

    public async Task RunAsync(
        CancellationToken cancellationToken = default)
    {
        var postUrlPath = Path.Combine(
            _output,
            "discovery",
            "post-urls.json");

        var postUrls = await _postUrlReader.ReadAsync(
            postUrlPath);

        _logger.Log();
        _logger.Log(
            $"Found {postUrls.Count} post URL(s) to recover.");

        foreach (var postUrl in postUrls)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await RecoverAsync(
                postUrl,
                cancellationToken);
        }

        _logger.Log();
        _logger.Log("Recovery step complete.");
    }

    private async Task RecoverAsync(
        string postUrl,
        CancellationToken cancellationToken)
    {
        var slug = GetSlug(postUrl);

        var postDirectory = Path.Combine(
            _output,
            "recovered",
            slug);

        Directory.CreateDirectory(postDirectory);

        var sourcePath = Path.Combine(
            postDirectory,
            "source.html");

        var capturePath = Path.Combine(
            postDirectory,
            "capture.json");

        // Idempotent: don't recover the same post twice.
        if (File.Exists(sourcePath) &&
            File.Exists(capturePath))
        {
            _logger.Log($"  SKIP: {postUrl}");
            _logger.Log("        Already recovered.");
            return;
        }

        _logger.Log();
        _logger.Log($"  Recovering: {postUrl}");

        var captures = await _wayback.GetCapturesAsync(
            postUrl,
            cancellationToken);

        if (captures.Count == 0)
        {
            _logger.Log("  No HTML captures found.");
            return;
        }

        _logger.Log(
            $"  Found {captures.Count} HTML capture(s).");

        foreach (var capture in captures
                     .OrderByDescending(x => x.Timestamp))
        {
            cancellationToken.ThrowIfCancellationRequested();

            _logger.Log(
                $"  Trying {capture.Timestamp}");

            var html = await _wayback.GetCaptureHtmlAsync(
                capture,
                cancellationToken);

            if (string.IsNullOrWhiteSpace(html))
            {
                _logger.Log("    Not usable.");
                continue;
            }

            await File.WriteAllTextAsync(
                sourcePath,
                html,
                cancellationToken);

            var json = JsonSerializer.Serialize(
                capture,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });

            await File.WriteAllTextAsync(
                capturePath,
                json,
                cancellationToken);

            _logger.Log("  SUCCESS");
            _logger.Log($"    HTML:    {sourcePath}");
            _logger.Log($"    Capture: {capturePath}");

            return;
        }

        _logger.Log(
            "  FAILED: No usable capture found.");
    }

    private static string GetSlug(string postUrl)
    {
        var uri = new Uri(postUrl);

        return uri.AbsolutePath
            .Trim('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Last();
    }
}