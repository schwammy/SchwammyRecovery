namespace SchwammyRecovery.Status;

public interface IPostStatusStore
{
    Task<Dictionary<string, PostStatusEntry>> LoadAsync(
        CancellationToken cancellationToken = default);

    Task<PostStatusEntry?> GetEntryAsync(
        string slug,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        Dictionary<string, PostStatusEntry> entries,
        CancellationToken cancellationToken = default);

    Task UpdateDiscoveredStatusesAsync(
        CancellationToken cancellationToken = default);

    Task UpdateStepStatusAsync(
        string slug,
        string stepName,
        string status,
        string? error = null,
        CancellationToken cancellationToken = default);
}
