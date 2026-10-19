using Core.Abstractions;
using Core.Domain;

namespace Core.Storage;

// Декоратор: перше читання отримує знімок; повторні читання не звертаються до inner.
// Зміни проходять через inner, після чого кеш скидається.
public sealed class CachingCatalogStore(ICatalogStore inner) : ICatalogStore
{
    private readonly ICatalogStore _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    private Dictionary<string, Goods>? _cache;

    public IReadOnlyList<Goods> List() => CatalogData.Snapshot(Items().Values);

    public Goods? GetById(string id)
    {
        string key = CatalogData.Key(id);
        return Items().TryGetValue(key, out Goods? item) ? CatalogData.Copy(item) : null;
    }

    public void Add(Goods item)
    {
        _inner.Add(item);
        _cache = null;
    }

    public void Update(Goods item)
    {
        _inner.Update(item);
        _cache = null;
    }

    public bool Remove(string id)
    {
        bool removed = _inner.Remove(id);
        _cache = null;
        return removed;
    }

    private Dictionary<string, Goods> Items() => _cache ??= CatalogData.Create(_inner.List());
}
