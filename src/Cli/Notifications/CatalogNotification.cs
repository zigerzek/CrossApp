using Core.Events;

namespace Cli.Notifications;

public static class CatalogNotification
{
    public static string Format(CatalogChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);
        return $"{change.At:HH:mm:ss} {change.Kind,-8} {change.Id} {change.Delta,4:+0;-0} → залишок {change.QuantityAfter}";
    }
}
