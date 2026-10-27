using Core.Events;

namespace Core.Abstractions;

// Альтернатива події: одержувач передається через конструктор.
// Реалізація відповідає за оброблення власних помилок введення/виведення.
public interface INotificationSink
{
    void Notify(CatalogChangedEventArgs change);
}
