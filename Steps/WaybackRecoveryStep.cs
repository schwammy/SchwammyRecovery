using SchwammyRecovery.Recovery;

namespace SchwammyRecovery.Steps;

public sealed class WaybackRecoveryStep : IStep
{
    private readonly IWaybackRecoveryService _waybackRecoveryService;

    public WaybackRecoveryStep(
        IWaybackRecoveryService waybackRecoveryService)
    {
        _waybackRecoveryService = waybackRecoveryService;
    }

    public async Task RunAsync(
        CancellationToken cancellationToken = default)
    {
        await _waybackRecoveryService.PrefetchArchivePagesAsync(cancellationToken);
        await _waybackRecoveryService.RecoverPostsAsync(cancellationToken);
    }
}