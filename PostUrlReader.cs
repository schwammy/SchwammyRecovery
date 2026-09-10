using System.Text.Json;

namespace SchwammyRecovery;

public sealed class PostUrlReader
{
    public async Task<IReadOnlyList<string>> ReadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException(
                "Post URL file was not found.",
                path);

        var json = await File.ReadAllTextAsync(
            path,
            cancellationToken);

        var urls = JsonSerializer.Deserialize<List<string>>(json);

        return urls ?? [];
    }
}