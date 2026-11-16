namespace Cli;

/// <summary>
/// Розбір «глобальних» прапорців (async-імпорт з лаб. 8 та перевизначення конфігурації з лаб. 9).
/// Решта аргументів (<see cref="Rest"/>) — команда та параметри сховища.
/// </summary>
internal sealed record CliArguments(
    string[] ConfigArgs,
    string[] Rest,
    bool UseStream,
    bool ImmediateCancel,
    TimeSpan? Timeout,
    bool ShowProgress)
{
    public static CliArguments Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        bool useStream = false, immediateCancel = false, showProgress = false;
        TimeSpan? timeout = null;
        var configArgs = new List<string>();
        var rest = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (arg == "--stream")
            {
                useStream = true;
            }
            else if (arg == "--cancel")
            {
                immediateCancel = true;
            }
            else if (arg == "--progress")
            {
                showProgress = true;
            }
            else if (arg == "--timeout" && i + 1 < args.Length)
            {
                string value = args[++i];
                if (value.EndsWith("ms", StringComparison.OrdinalIgnoreCase) && int.TryParse(value[..^2], out int ms))
                    timeout = TimeSpan.FromMilliseconds(ms);
                else if (value.EndsWith('s') && double.TryParse(value[..^1], out double seconds))
                    timeout = TimeSpan.FromSeconds(seconds);
                else if (double.TryParse(value, out double number))
                    timeout = TimeSpan.FromSeconds(number);
            }
            else if (IsConfigOverride(arg))
            {
                configArgs.Add(arg); // --Storage:DataPath=tmp → джерело AddCommandLine
            }
            else
            {
                rest.Add(arg);
            }
        }

        return new CliArguments(configArgs.ToArray(), rest.ToArray(), useStream, immediateCancel, timeout, showProgress);
    }

    private static bool IsConfigOverride(string arg) =>
        arg.StartsWith("--", StringComparison.Ordinal) && arg.Contains(':') && arg.Contains('=');
}
