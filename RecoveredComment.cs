namespace SchwammyRecovery;

public sealed class RecoveredComment
{
    public string? Id { get; init; }
    public string Type { get; init; } = "comment";
    public string? Author { get; init; }
    public string? AuthorUrl { get; init; }
    public string? Date { get; init; }
    public string? Content { get; init; }
    public string? Url { get; init; }
    public string? ParentId { get; init; }
}