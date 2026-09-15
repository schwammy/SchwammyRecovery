using SchwammyRecovery;
using Microsoft.Extensions.DependencyInjection;
using SchwammyRecovery.Steps;
using SchwammyRecovery.Extraction;
using SchwammyRecovery.Conversion;
using SchwammyRecovery.Recovery;
using SchwammyRecovery.Status;
using SchwammyRecovery.Export;

var outputDirectory = "output";

using var logger = new Logger(outputDirectory);

using var http = new HttpClient(new HttpClientHandler
{
    AutomaticDecompression = System.Net.DecompressionMethods.All
});

http.DefaultRequestHeaders.UserAgent.ParseAdd(
    "SchwammyRecovery/0.1 (+personal blog recovery project)");

var services = new ServiceCollection();

services.AddSingleton(outputDirectory);
services.AddSingleton(logger);
services.AddSingleton(http);
services.AddSingleton<IPostStatusStore>(new PostStatusStore(outputDirectory));
services.AddTransient<PostUrlReader>();
services.AddTransient<IOutstandingPostsReviewService, OutstandingPostsReviewService>();
services.AddTransient<IWaybackRecoveryService, WaybackRecoveryService>();
services.AddTransient<WaybackRecoveryStep>();
services.AddTransient<ReviewOutstandingPostsStep>();
services.AddSingleton<WaybackClient>();
services.AddScoped<IRecoveredPostEnumerationService, RecoveredPostEnumerationService>();
services.AddScoped<IWordPressPostExtractor, WordPressPostExtractor>();
services.AddScoped<IWordPressCommentExtractor, WordPressCommentExtractor>();
services.AddScoped<IImageExtractor, ImageExtractor>();
services.AddScoped<ICodeAnalysisExtractor, CodeAnalysisExtractor>();
services.AddScoped<IImageDownloader, ImageDownloader>();
services.AddScoped<IExtractedPostEnumerationService, ExtractedPostEnumerationService>();
services.AddScoped<IHtmlToMarkdownConverter, HtmlToMarkdownConverter>();
services.AddTransient<MarkdownConversionStep>();
services.AddTransient<PortableMarkdownExportStep>();

using var serviceProvider = services.BuildServiceProvider();

var wayback = new WaybackClient(http, logger);

var discoveryDirectory = Path.Combine(
    outputDirectory,
    "discovery");

var crawler = new ArchiveCrawler(
    wayback,
    discoveryDirectory,
    logger);

var discoveryStep = new DiscoveryStep(
    crawler,
    serviceProvider.GetRequiredService<IPostStatusStore>());
var waybackRecoveryStep = serviceProvider.GetRequiredService<WaybackRecoveryStep>();

var extractionStep = new ExtractionStep(serviceProvider.GetRequiredService<IRecoveredPostEnumerationService>(),
    serviceProvider.GetRequiredService<IWordPressPostExtractor>(),
    serviceProvider.GetRequiredService<IWordPressCommentExtractor>(),
    serviceProvider.GetRequiredService<IImageExtractor>(),
    serviceProvider.GetRequiredService<ICodeAnalysisExtractor>(),
    outputDirectory,
    logger,
    serviceProvider.GetRequiredService<IPostStatusStore>());

var extractedDirectory = Path.Combine(
    outputDirectory,
    "extracted");

var downloadStep = new ImageDownloadStep(
    serviceProvider.GetRequiredService<IExtractedPostEnumerationService>(),
    serviceProvider.GetRequiredService<IImageDownloader>(),
    extractedDirectory,
    logger,
    serviceProvider.GetRequiredService<IPostStatusStore>());

var conversionStep = new MarkdownConversionStep(
    serviceProvider.GetRequiredService<IExtractedPostEnumerationService>(),
    serviceProvider.GetRequiredService<IHtmlToMarkdownConverter>(),
    outputDirectory,
    logger,
    serviceProvider.GetRequiredService<IPostStatusStore>());

var reviewOutstandingPostsStep = serviceProvider.GetRequiredService<ReviewOutstandingPostsStep>();
var portableMarkdownExportStep = serviceProvider.GetRequiredService<PortableMarkdownExportStep>();

while (true)
{
    logger.Log("Schwammy Recovery");
    logger.Log("=================");
    logger.Log();
    logger.Log("1. Discover post URLs");
    logger.Log("2. Recover Wayback HTML");
    logger.Log("3. Extract post content");
    logger.Log("4. Recover images");
    logger.Log("5. Convert to Markdown");
    logger.Log("6. Review outstanding posts");
    logger.Log("7. Create portable Markdown export");
    logger.Log("Q. Quit");
    logger.Log();
    logger.Log("Select an option: ");

    var choice = Console.ReadLine()?.Trim().ToUpperInvariant();

    logger.Log($"Selected option: {choice}");
    logger.Log();

    switch (choice)
    {
        case "1":
            logger.Log("Enter the archive year (leave blank to use the default 2007):");
            var yearText = Console.ReadLine();

            logger.Log("Enter the start month (leave blank to use the default 04):");
            var startMonthText = Console.ReadLine();

            logger.Log("Enter the end month (leave blank to use the start month):");
            var endMonthText = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(yearText) &&
                string.IsNullOrWhiteSpace(startMonthText) &&
                string.IsNullOrWhiteSpace(endMonthText))
            {
                await discoveryStep.RunAsync();
            }
            else
            {
                var year = int.TryParse(yearText, out var parsedYear)
                    ? parsedYear
                    : 2007;

                var startMonth = int.TryParse(startMonthText, out var parsedStartMonth)
                    ? parsedStartMonth
                    : 4;

                var endMonth = int.TryParse(endMonthText, out var parsedEndMonth)
                    ? parsedEndMonth
                    : startMonth;

                await discoveryStep.RunAsync(year, startMonth, endMonth);
            }
            break;
        case "2":
            await waybackRecoveryStep.RunAsync();
            break;

        case "3":
            await extractionStep.RunAsync();
            break;
        case "4":
            await downloadStep.RunAsync();
            break;
        case "5":
            await conversionStep.RunAsync();
            break;
        case "6":
            await reviewOutstandingPostsStep.RunAsync();
            break;

        case "7":
            await portableMarkdownExportStep.RunAsync();
            break;

        case "Q":
            return;

        default:
            logger.Log("Invalid selection.");
            break;
    }

    logger.Log();
    logger.Log("Press ENTER to return to the menu.");
    Console.ReadLine();
}


