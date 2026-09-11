namespace SchwammyRecovery.Steps;

public interface IStep
{
    Task RunAsync(
        CancellationToken cancellationToken = default);
}