using Core.Abstractions;
using Core.Events;

namespace Cli.Notifications;

public sealed class ConsoleSink(TextWriter? output = null, Action<string>? onError = null) : INotificationSink
{
    private readonly TextWriter _output = output ?? Console.Out;
    private readonly Action<string> _onError = onError ?? Console.Error.WriteLine;
    public int FailureCount { get; private set; }

    public void Notify(CatalogChangedEventArgs change)
    {
        string line = CatalogNotification.Format(change);
        try { _output.WriteLine(line); }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            FailureCount++;
            _onError($"Не вдалося вивести сповіщення: {ex.Message}");
        }
    }
}
