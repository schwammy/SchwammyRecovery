using System.Threading;
using System.Threading.Tasks;

namespace SchwammyRecovery.Steps;

public sealed class DiscoveryStep : IStep
{
    private readonly ArchiveCrawler _crawler;

    public DiscoveryStep(ArchiveCrawler crawler)
    {
        _crawler = crawler;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var startUrl =
           "https://web.archive.org/web/20220925020544/" +
           "http://www.schwammysays.net/2007/03/";

        await _crawler.CrawlArchiveAsync(startUrl);
    }
}