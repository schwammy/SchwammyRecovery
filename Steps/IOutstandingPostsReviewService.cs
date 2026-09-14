namespace SchwammyRecovery.Steps;

public interface IOutstandingPostsReviewService
{
    Task<OutstandingPostsReviewResult> ReviewAsync(
        string outputDirectory,
        CancellationToken cancellationToken = default);
}
