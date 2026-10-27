using System.Text;
using Core.Abstractions;
using Core.Events;

namespace Cli.Notifications;

public sealed class FileSink : INotificationSink
{
    private readonly Action<string> _onError;
    public string LogPath { get; }
    public int FailureCount { get; private set; }

    public FileSink(string path, Action<string>? onError = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        LogPath = Path.GetFullPath(path);
        _onError = onError ?? Console.Error.WriteLine;
    }

    public void Notify(CatalogChangedEventArgs change)
    {
        string line = CatalogNotification.Format(change);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, line + Environment.NewLine, new UTF8Encoding(false));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            FailureCount++;
            // Зміна вже збережена: збій журналу не повинен зупинити наступного підписника.
            _onError($"Не вдалося записати журнал {LogPath}: {ex.Message}");
        }
    }
}
