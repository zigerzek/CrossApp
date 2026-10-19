using Core.Abstractions;
using Core.Domain;

namespace Core.Services;

public sealed class CatalogService(ICatalogStore store)
{
    private readonly ICatalogStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public Goods Add(string sku, string name, string unit, int quantity, string? note = null)
    {
        Goods item = Goods.Create(Guid.NewGuid().ToString("N"), sku, name, unit, quantity, note);
        _store.Add(item);
        return item;
    }

    public void Receive(string id, int quantity)
    {
        Goods item = Require(id);
        item.RegisterArrival(quantity);
        _store.Update(item);
    }

    public void Issue(string id, int quantity)
    {
        Goods item = Require(id);
        item.Issue(quantity);
        _store.Update(item);
    }

    public IReadOnlyList<Goods> All() => _store.List();
    public Goods? Find(string id) => _store.GetById(id);
    public bool Remove(string id) => _store.Remove(id);

    public IReadOnlyList<Goods> Search(Func<Goods, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return _store.List().Where(predicate).ToList().AsReadOnly();
    }

    private Goods Require(string id) => _store.GetById(id)
        ?? throw new InvalidOperationException($"Товар з id={id} не знайдено.");
}
