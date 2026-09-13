namespace SchwammyRecovery.Recovery;

public sealed class RecoveryProvenance
{
    public string PostUrl { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string? ArchivePageUrl { get; set; }
    public string? ArchivePagePath { get; set; }
    public DateTimeOffset RecoveredAtUtc { get; set; }
    public string? Notes { get; set; }
}
