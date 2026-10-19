using Core.Domain;

namespace Core.Storage;

internal static class CatalogData
{
    internal static Goods Copy(Goods item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Goods.FromDto(item.ToDto());
    }

    internal static string Key(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return id.Trim();
    }

    internal static Dictionary<string, Goods> Create(IEnumerable<Goods>? items = null)
    {
        var result = new Dictionary<string, Goods>(StringComparer.OrdinalIgnoreCase);
        foreach (Goods item in items ?? [])
            Add(result, item);
        return result;
    }

    internal static void Add(Dictionary<string, Goods> items, Goods item)
    {
        Goods copy = Copy(item);
        if (!items.TryAdd(copy.Id, copy))
            throw new InvalidOperationException($"Запис з id={copy.Id} уже існує.");
    }

    internal static void Update(Dictionary<string, Goods> items, Goods item)
    {
        Goods copy = Copy(item);
        if (!items.ContainsKey(copy.Id))
            throw new InvalidOperationException($"Немає запису з id={copy.Id} для оновлення.");
        items[copy.Id] = copy;
    }

    internal static IReadOnlyList<Goods> Snapshot(IEnumerable<Goods> items) =>
        items.Select(Copy).ToList().AsReadOnly();
}
