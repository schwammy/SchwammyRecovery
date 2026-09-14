namespace SchwammyRecovery.Status;

public sealed class PostStatusEntry
{
    public string PostUrl { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string DiscoveredStatus { get; set; } = "N";

    public string RecoveredStatus { get; set; } = "N";

    public string ExtractedStatus { get; set; } = "N";

    public string ImagesStatus { get; set; } = "N";

    public string MarkdownStatus { get; set; } = "N";

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public string? LastError { get; set; }
}
