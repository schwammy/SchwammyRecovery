using SchwammyRecovery;

var startUrl = args.Length > 0
    ? args[0]
    : "https://web.archive.org/web/20220925020544/http://www.schwammysays.net/2007/03/";

var outputDirectory = args.Length > 1 ? args[1] : "output";

using var http = new HttpClient(new HttpClientHandler
{
    AutomaticDecompression = System.Net.DecompressionMethods.All
});

http.DefaultRequestHeaders.UserAgent.ParseAdd(
    "SchwammyRecovery/0.1 (+personal blog recovery project)");

var client = new WaybackClient(http);
var crawler = new ArchiveCrawler(client, outputDirectory);

await crawler.CrawlArchiveAsync(startUrl);

Console.WriteLine();
Console.WriteLine($"Done. Results are in: {Path.GetFullPath(outputDirectory)}");
