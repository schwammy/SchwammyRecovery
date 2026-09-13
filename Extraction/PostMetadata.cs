using SchwammyRecovery.Recovery;

namespace SchwammyRecovery;

public sealed class PostMetadata
{
    public required string Title { get; init; }
    public required string Slug { get; init; }
    public string? Published { get; init; }
    public string? Author { get; init; }
    public List<string> Categories { get; init; } = [];
    public required string SourceUrl { get; init; }
    public required WaybackCapture Source { get; init; }
    public RecoveryProvenance? Provenance { get; init; }
}