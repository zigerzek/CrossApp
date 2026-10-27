using Core.Abstractions;
using Core.Domain;
using Core.Events;

namespace Core.Services;

public sealed class CatalogService(ICatalogStore store, INotificationSink? notificationSink = null)
{
    private readonly ICatalogStore _store = store ?? throw new ArgumentNullException(nameof(store));
    public event EventHandler<CatalogChangedEventArgs>? Changed;

    public Goods Add(string sku, string name, string unit, int quantity, string? note = null)
    {
        Goods item = Goods.Create(Guid.NewGuid().ToString("N"), sku, name, unit, quantity, note);
        _store.Add(item);
        OnChanged(item, CatalogChangeKind.Added, quantity);
        return item;
    }

    public void Receive(string id, int quantity)
    {
        Goods item = Require(id);
        item.RegisterArrival(quantity);
        _store.Update(item);
        OnChanged(item, CatalogChangeKind.Received, quantity);
    }

    public void Issue(string id, int quantity)
    {
        Goods item = Require(id);
        item.Issue(quantity);
        _store.Update(item);
        OnChanged(item, CatalogChangeKind.Issued, -quantity);
    }

    public IReadOnlyList<Goods> All() => _store.List();
    public Goods? Find(string id) => _store.GetById(id);
    public bool Remove(string id)
    {
        Goods? item = _store.GetById(id);
        if (item is null || !_store.Remove(id)) return false;
        OnChanged(item, CatalogChangeKind.Removed, -item.Quantity);
        return true;
    }

    public IReadOnlyList<Goods> Search(Func<Goods, bool> predicate) => FindAll(predicate);

    public IReadOnlyList<Goods> FindAll(Func<Goods, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return _store.List().Where(predicate).ToList().AsReadOnly();
    }

    private void OnChanged(Goods item, CatalogChangeKind kind, int delta)
    {
        var change = new CatalogChangedEventArgs(item.Id, kind, delta,
            kind == CatalogChangeKind.Removed ? 0 : item.Quantity);
        Changed?.Invoke(this, change);
        notificationSink?.Notify(change);
    }

    private Goods Require(string id) => _store.GetById(id)
        ?? throw new InvalidOperationException($"Товар з id={id} не знайдено.");
}
