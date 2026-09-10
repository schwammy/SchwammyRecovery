using SchwammyRecovery;

var outputDirectory = "output";

using var http = new HttpClient(new HttpClientHandler
{
    AutomaticDecompression = System.Net.DecompressionMethods.All
});

http.DefaultRequestHeaders.UserAgent.ParseAdd(
    "SchwammyRecovery/0.1 (+personal blog recovery project)");

var wayback = new WaybackClient(http);

var recovery = new WaybackRecoveryStep(
    wayback,
    outputDirectory);

await recovery.RecoverAsync(
    "http://www.schwammysays.net/have-you-checked-out-resharper/");

Console.WriteLine();
Console.WriteLine(
    $"Done. Results are in: {Path.GetFullPath(outputDirectory)}");