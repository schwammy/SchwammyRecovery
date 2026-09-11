namespace SchwammyRecovery;

public sealed class WaybackCapture
{
    public required string Timestamp { get; init; }
    public required string OriginalUrl { get; init; }
    public required string CaptureUrl { get; init; }

    public string? StatusCode { get; init; }
    public string? MimeType { get; init; }
    public string? Digest { get; init; }
}