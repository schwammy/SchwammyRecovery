namespace SchwammyRecovery.Steps;

public sealed class MissingImageReport
{
    public IReadOnlyList<string> LocalImages { get; init; } = [];

    public IReadOnlyList<string> ExternalImages { get; init; } = [];
}

public sealed class OutstandingPostsReviewResult
{
    public int DiscoveredCount { get; init; }

    public int RecoveredCount { get; init; }

    public IReadOnlyList<string> OutstandingPosts { get; init; } = [];

    public IReadOnlyDictionary<string, MissingImageReport> MissingImagePosts { get; init; } =
        new Dictionary<string, MissingImageReport>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> MissingImageFiles { get; init; } = [];

    public IReadOnlyList<string> MissingLocalImageFiles { get; init; } = [];

    public IReadOnlyList<string> MissingExternalImageFiles { get; init; } = [];
}
