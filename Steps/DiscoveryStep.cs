using System.Threading;
using System.Threading.Tasks;
using SchwammyRecovery.Status;

namespace SchwammyRecovery.Steps;

public sealed class DiscoveryStep : IStep
{
    private const string BaseUrl =
        "https://web.archive.org/web/20220925020544/" +
        "http://www.schwammysays.net/";

    private const int DelaySecondsBetweenMonths = 2;

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
        return RunAsync(2007, 4, 4, cancellationToken);
    }

    public Task RunAsync(
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(year, month, month, cancellationToken);
    }

    public async Task RunAsync(
        int year,
        int startMonth,
        int endMonth,
        CancellationToken cancellationToken = default)
    {
        if (startMonth < 1 || startMonth > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(startMonth));
        }

        if (endMonth < 1 || endMonth > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(endMonth));
        }

        if (endMonth < startMonth)
        {
            throw new ArgumentException(
                "The end month must be greater than or equal to the start month.");
        }

        for (var month = startMonth; month <= endMonth; month++)
        {
            var startUrl = $"{BaseUrl}{year:0000}/{month:00}/";

            await _crawler.CrawlArchiveAsync(startUrl, cancellationToken);

            if (month < endMonth)
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(DelaySecondsBetweenMonths),
                    cancellationToken);
            }
        }

        await _postStatusStore.UpdateDiscoveredStatusesAsync(cancellationToken);
    }
}