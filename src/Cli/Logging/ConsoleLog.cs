namespace Cli.Logging;

internal enum LogLevel
{
    Trace,
    Debug,
    Information,
    Warning,
    Error
}

/// <summary>Мінімалістичний консольний журнал: рівень береться з Logging:MinimumLevel.</summary>
internal sealed class ConsoleLog(LogLevel minimum)
{
    public LogLevel Minimum { get; } = minimum;

    public static ConsoleLog FromOptions(string? level) =>
        new(Enum.TryParse(level, ignoreCase: true, out LogLevel parsed) ? parsed : LogLevel.Information);

    public void Trace(string message) => Write(LogLevel.Trace, message);
    public void Debug(string message) => Write(LogLevel.Debug, message);
    public void Info(string message) => Write(LogLevel.Information, message);
    public void Warn(string message) => Write(LogLevel.Warning, message);

    private void Write(LogLevel level, string message)
    {
        if (level < Minimum) return;
        Console.WriteLine($"[{level.ToString().ToLowerInvariant()}] {message}");
    }
}
