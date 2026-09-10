namespace SchwammyRecovery;

public sealed class Logger : IDisposable
{
    private readonly StreamWriter _writer;

    public Logger(string outputDirectory)
    {
        var logDirectory = Path.Combine(
            outputDirectory,
            "logs");

        Directory.CreateDirectory(logDirectory);

        var timestamp = DateTime.Now.ToString(
            "yyyyMMdd-HHmmss");

        var logPath = Path.Combine(
            logDirectory,
            $"{timestamp}.log");

        _writer = new StreamWriter(
            logPath,
            append: false)
        {
            AutoFlush = true
        };

        Log($"Schwammy Recovery started");
        Log($"Log file: {logPath}");
    }

    public void Log(string message = "")
    {
        Console.WriteLine(message);
        _writer.WriteLine(message);
    }

    public void Dispose()
    {
        _writer.Dispose();
    }
}

