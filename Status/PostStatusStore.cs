using System.Text.Json;

namespace SchwammyRecovery.Status;

public sealed class PostStatusStore : IPostStatusStore
{
    private readonly string _statusPath;

    public PostStatusStore(string outputDirectory)
    {
        _statusPath = Path.Combine(outputDirectory, "post-status.json");
    }

    public async Task<Dictionary<string, PostStatusEntry>> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_statusPath))
        {
            return new Dictionary<string, PostStatusEntry>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var json = await File.ReadAllTextAsync(_statusPath, cancellationToken);

            if (string.IsNullOrWhiteSpace(json))
            {
                return new Dictionary<string, PostStatusEntry>(StringComparer.OrdinalIgnoreCase);
            }

            var entries = JsonSerializer.Deserialize<Dictionary<string, PostStatusEntry>>(json);

            return entries ?? new Dictionary<string, PostStatusEntry>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, PostStatusEntry>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public async Task<PostStatusEntry?> GetEntryAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return null;
        }

        var entries = await LoadAsync(cancellationToken);

        return entries.TryGetValue(slug, out var entry)
            ? entry
            : null;
    }

    public async Task SaveAsync(
        Dictionary<string, PostStatusEntry> entries,
        CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_statusPath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(
            entries,
            new JsonSerializerOptions { WriteIndented = true });

        await File.WriteAllTextAsync(_statusPath, json, cancellationToken);
    }

    public async Task UpdateDiscoveredStatusesAsync(
        CancellationToken cancellationToken = default)
    {
        var discoveryDirectory = Path.Combine(
            Path.GetDirectoryName(_statusPath) ?? string.Empty,
            "discovery");

        var postUrlsPath = Path.Combine(discoveryDirectory, "post-urls.json");

        if (!File.Exists(postUrlsPath))
        {
            return;
        }

        var json = await File.ReadAllTextAsync(postUrlsPath, cancellationToken);

        var postUrls = JsonSerializer.Deserialize<string[]>(json) ?? [];

        var entries = await LoadAsync(cancellationToken);

        foreach (var postUrl in postUrls)
        {
            if (string.IsNullOrWhiteSpace(postUrl))
            {
                continue;
            }

            var slug = GetSlug(postUrl);

            if (string.IsNullOrWhiteSpace(slug))
            {
                continue;
            }

            var entry = entries.TryGetValue(slug, out var existing)
                ? existing
                : new PostStatusEntry
                {
                    Slug = slug
                };

            entry.PostUrl = postUrl;
            entry.Slug = slug;
            entry.DiscoveredStatus = "S";
            entry.UpdatedAtUtc = DateTimeOffset.UtcNow;

            entries[slug] = entry;
        }

        await SaveAsync(entries, cancellationToken);
    }

    public async Task UpdateStepStatusAsync(
        string slug,
        string stepName,
        string status,
        string? error = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return;
        }

        var entries = await LoadAsync(cancellationToken);

        var entry = entries.TryGetValue(slug, out var existing)
            ? existing
            : new PostStatusEntry
            {
                Slug = slug
            };

        entry.Slug = slug;
        entry.UpdatedAtUtc = DateTimeOffset.UtcNow;

        switch (stepName)
        {
            case "DiscoveredStatus":
                entry.DiscoveredStatus = status;
                break;
            case "RecoveredStatus":
                entry.RecoveredStatus = status;
                break;
            case "ExtractedStatus":
                entry.ExtractedStatus = status;
                break;
            case "ImagesStatus":
                entry.ImagesStatus = status;
                break;
            case "MarkdownStatus":
                entry.MarkdownStatus = status;
                break;
        }

        entry.LastError = status == "F" ? error : null;

        entries[slug] = entry;

        await SaveAsync(entries, cancellationToken);
    }

    private static string GetSlug(string postUrl)
    {
        try
        {
            var uri = new Uri(postUrl);

            return uri.AbsolutePath
                .Trim('/')
                .Split(
                    '/',
                    StringSplitOptions.RemoveEmptyEntries)
                .Last();
        }
        catch
        {
            return postUrl;
        }
    }
}
