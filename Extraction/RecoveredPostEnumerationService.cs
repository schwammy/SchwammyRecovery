namespace SchwammyRecovery.Steps;

public interface IRecoveredPostEnumerationService
{
    IReadOnlyList<string> Enumerate(string recoveredDirectory);
}

public sealed class RecoveredPostEnumerationService
    : IRecoveredPostEnumerationService
{
    public IReadOnlyList<string> Enumerate(
        string recoveredDirectory)
    {
        if (!Directory.Exists(recoveredDirectory))
        {
            return [];
        }

        // Each recovered post is represented by its own directory.
        // We use the directory name as the slug because Recovery
        // already established this as the post's filesystem identity.
        return Directory
               .EnumerateDirectories(recoveredDirectory)
               .Where(IsRecoveredPost)
               .Select(Path.GetFileName)
               .Where(slug => !string.IsNullOrWhiteSpace(slug))
               .OrderBy(slug => slug)
               .ToList()!;
    }

    private static bool IsRecoveredPost(string directory)
    {
        // Recovery writes these two files together. Requiring both
        // prevents an incomplete recovery directory from entering
        // the extraction pipeline.
        return File.Exists(
                   Path.Combine(directory, "source.html"))
            && File.Exists(
                   Path.Combine(directory, "capture.json"));
    }
}
