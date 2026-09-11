using SchwammyRecovery;
using Microsoft.Extensions.DependencyInjection;
using SchwammyRecovery.Steps;

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
services.AddTransient<PostUrlReader>();
services.AddTransient<WaybackRecoveryStep>();
services.AddSingleton<WaybackClient>();

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
    crawler);
var waybackRecoveryStep = serviceProvider.GetRequiredService<WaybackRecoveryStep>();

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
    logger.Log("6. Review recovered posts");
    logger.Log("7. Export to Ghost");
    logger.Log("Q. Quit");
    logger.Log();
    logger.Log("Select an option: ");

    var choice = Console.ReadLine()?.Trim().ToUpperInvariant();

    logger.Log($"Selected option: {choice}");
    logger.Log();

    switch (choice)
    {
        case "1":
            await discoveryStep.RunAsync();
            break;
        case "2":
            await waybackRecoveryStep.RunAsync();
            break;

        case "3":
            await RunExtractionAsync(
                outputDirectory,
                logger);
            break;
        case "4":
        case "5":
        case "6":
        case "7":
            logger.Log("Not implemented yet.");
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


static async Task RunExtractionAsync(
    string outputDirectory,
    Logger logger)
{
    var postUrlPath = Path.Combine(
        outputDirectory,
        "discovery",
        "post-urls.json");

    var reader = new PostUrlReader();

    var postUrls = await reader.ReadAsync(postUrlPath);

    var extraction = new PostExtractionStep(
        outputDirectory,
        logger);

    foreach (var postUrl in postUrls)
    {
        var uri = new Uri(postUrl);

        var slug = uri.AbsolutePath
            .Trim('/')
            .Split('/')
            .Last();

        if (string.IsNullOrWhiteSpace(slug))
        {
            logger.Log(
                $"Skipping URL with no slug: {postUrl}");

            continue;
        }

        await extraction.ExtractAsync(slug);
    }

    logger.Log();
    logger.Log("Extraction step complete.");
}
