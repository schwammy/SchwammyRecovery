using System.Text.Json;

namespace SchwammyRecovery;

public sealed class WaybackRecoveryStep
{
    private readonly WaybackClient _wayback;
    private readonly string _output;
    private readonly Logger _logger;
    public WaybackRecoveryStep(
        WaybackClient wayback,
        string output,
        Logger logger)
    {
        _wayback = wayback;
        _output = output;
        _logger = logger;
    }

    public async Task RecoverAsync(
        string postUrl,
        CancellationToken cancellationToken = default)
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

            Directory.CreateDirectory(postDirectory);

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

    private static string GetSlug(string url)
    {
        var uri = new Uri(url);

        var slug = uri.AbsolutePath
            .Trim('/')
            .Split('/')
            .Last();

        return string.IsNullOrWhiteSpace(slug)
            ? "unknown-post"
            : slug;
    }
}