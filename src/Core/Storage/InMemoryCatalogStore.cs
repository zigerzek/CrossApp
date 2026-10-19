using Core.Abstractions;
using Core.Domain;

namespace Core.Storage;

public sealed class InMemoryCatalogStore(IEnumerable<Goods>? seed = null) : ICatalogStore
{
    private readonly Dictionary<string, Goods> _items = CatalogData.Create(seed);

    public IReadOnlyList<Goods> List() => CatalogData.Snapshot(_items.Values);

    public Goods? GetById(string id) =>
        _items.TryGetValue(CatalogData.Key(id), out Goods? item) ? CatalogData.Copy(item) : null;

    public void Add(Goods item) => CatalogData.Add(_items, item);
    public void Update(Goods item) => CatalogData.Update(_items, item);
    public bool Remove(string id) => _items.Remove(CatalogData.Key(id));
}
