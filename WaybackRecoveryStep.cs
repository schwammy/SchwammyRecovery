using System.Text.Json;

namespace SchwammyRecovery;

public sealed class WaybackRecoveryStep
{
    private readonly WaybackClient _wayback;
    private readonly string _output;

    public WaybackRecoveryStep(
        WaybackClient wayback,
        string output)
    {
        _wayback = wayback;
        _output = output;
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
            Console.WriteLine($"  SKIP: {postUrl}");
            Console.WriteLine("        Already recovered.");
            return;
        }

        Console.WriteLine();
        Console.WriteLine($"  Recovering: {postUrl}");

        var captures = await _wayback.GetCapturesAsync(
            postUrl,
            cancellationToken);

        if (captures.Count == 0)
        {
            Console.WriteLine("  No HTML captures found.");
            return;
        }

        Console.WriteLine(
            $"  Found {captures.Count} HTML capture(s).");

        foreach (var capture in captures
                     .OrderByDescending(x => x.Timestamp))
        {
            cancellationToken.ThrowIfCancellationRequested();

            Console.WriteLine(
                $"  Trying {capture.Timestamp}");

            var html = await _wayback.GetCaptureHtmlAsync(
                capture,
                cancellationToken);

            if (string.IsNullOrWhiteSpace(html))
            {
                Console.WriteLine("    Not usable.");
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

            Console.WriteLine("  SUCCESS");
            Console.WriteLine($"    HTML:    {sourcePath}");
            Console.WriteLine($"    Capture: {capturePath}");

            return;
        }

        Console.WriteLine(
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