using System.Threading;
using System.Threading.Tasks;
using SchwammyRecovery.Status;

namespace SchwammyRecovery.Steps;

public sealed class DiscoveryStep : IStep
{
    private const string BaseUrl =
        "https://web.archive.org/web/20220925020544/" +
        "http://www.schwammysays.net/";

    private readonly ArchiveCrawler _crawler;
    private readonly IPostStatusStore _postStatusStore;

    public DiscoveryStep(
        ArchiveCrawler crawler,
        IPostStatusStore postStatusStore)
    {
        _crawler = crawler;
        _postStatusStore = postStatusStore;
    }

    public Task RunAsync(CancellationToken cancellationToken = default)
    {
        return RunAsync(2007, 4, cancellationToken);
    }

    public async Task RunAsync(
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        var startUrl = $"{BaseUrl}{year:0000}/{month:00}/";

        await _crawler.CrawlArchiveAsync(startUrl, cancellationToken);

        await _postStatusStore.UpdateDiscoveredStatusesAsync(cancellationToken);
    }
}