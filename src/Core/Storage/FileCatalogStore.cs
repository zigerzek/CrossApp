using System.Text.Json;
using Core.Abstractions;
using Core.Domain;

namespace Core.Storage;

public sealed class FileCatalogStore : ICatalogStore
{
    private readonly string _path;
    private Dictionary<string, Goods> _cache = CatalogData.Create();
    private bool _loaded;

    public FileCatalogStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = Path.GetFullPath(path);
    }

    /// <summary>Повний шлях до JSON-файлу, з яким працює сховище.</summary>
    public string FilePath => _path;

    public IReadOnlyList<Goods> List()
    {
        EnsureLoaded();
        return CatalogData.Snapshot(_cache.Values);
    }

    public Goods? GetById(string id)
    {
        string key = CatalogData.Key(id);
        EnsureLoaded();
        return _cache.TryGetValue(key, out Goods? item) ? CatalogData.Copy(item) : null;
    }

    public void Add(Goods item)
    {
        ArgumentNullException.ThrowIfNull(item);
        EnsureLoaded();
        var next = new Dictionary<string, Goods>(_cache, StringComparer.OrdinalIgnoreCase);
        CatalogData.Add(next, item);
        Commit(next);
    }

    public void Update(Goods item)
    {
        ArgumentNullException.ThrowIfNull(item);
        EnsureLoaded();
        var next = new Dictionary<string, Goods>(_cache, StringComparer.OrdinalIgnoreCase);
        CatalogData.Update(next, item);
        Commit(next);
    }

    public bool Remove(string id)
    {
        string key = CatalogData.Key(id);
        EnsureLoaded();
        var next = new Dictionary<string, Goods>(_cache, StringComparer.OrdinalIgnoreCase);
        if (!next.Remove(key)) return false;
        Commit(next);
        return true;
    }

    private void EnsureLoaded()
    {
        if (_loaded) return;
        string json;
        try { json = File.ReadAllText(_path); }
        catch (FileNotFoundException) { _loaded = true; return; }
        catch (DirectoryNotFoundException) { _loaded = true; return; }

        try
        {
            var dtos = CatalogJson.Deserialize(json);
            // Публікуємо кеш лише після перевірки всього файлу.
            _cache = CatalogData.Create(dtos.Select(Goods.FromDto));
            _loaded = true;
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidOperationException)
        {
            throw new InvalidDataException($"Некоректний каталог '{_path}': {ex.Message}", ex);
        }
    }

    private void Commit(Dictionary<string, Goods> next)
    {
        // Формат і атомарний запис (.tmp + File.Move) — у CatalogJson, спільні з експортом.
        CatalogJson.WriteAtomic(_path, next.Values);
        _cache = next; // Невдалий запис не змінює кеш.
    }
}
