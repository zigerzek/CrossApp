using System.Text.Json;
using Cli.Composition;
using Core;
using Core.Abstractions;
using Core.Domain;
using Core.Dto;
using Core.Services;
using Core.Storage;

int passed = 0, failed = 0;
string root = Path.Combine(Path.GetTempPath(), $"crossapp-lab05-checks-{Guid.NewGuid():N}");
Directory.CreateDirectory(root);
try
{
    foreach (string mode in new[] { "memory", "file", "cached-memory", "cached-file" })
    {
        Check($"{mode}: CRUD, case-insensitive keys and missing records", () =>
        {
            ICatalogStore store = MakeStore(mode);
            Equal(0, store.List().Count);
            True(store.GetById("missing") is null);
            True(!store.Remove("missing"));
            Goods item = MakeGoods();
            store.Add(item);
            Equal(item.ToDto(), store.GetById(" g-1 ")!.ToDto());
            Goods edit = store.GetById("G-1")!;
            edit.RegisterArrival(7);
            store.Update(edit);
            Equal(17, store.GetById("g-1")!.Quantity);
            True(store.Remove("g-1"));
            True(!store.Remove("G-1"));
            Equal(0, store.List().Count);
        });
        Check($"{mode}: duplicate and missing Update leave data unchanged", () =>
        {
            ICatalogStore store = MakeStore(mode);
            store.Add(MakeGoods());
            Throws<InvalidOperationException>(() => store.Add(MakeGoods("g-1", 99)));
            Throws<InvalidOperationException>(() => store.Update(MakeGoods("other")));
            Equal(1, store.List().Count);
            Equal(10, store.GetById("G-1")!.Quantity);
        });
        Check($"{mode}: inputs and results cannot mutate storage indirectly", () =>
        {
            ICatalogStore store = MakeStore(mode);
            Goods input = MakeGoods();
            store.Add(input);
            input.RegisterArrival(100);
            Goods read = store.GetById("G-1")!;
            read.RegisterArrival(200);
            IReadOnlyList<Goods> list = store.List();
            list[0].RegisterArrival(300);
            Throws<NotSupportedException>(() => ((IList<Goods>)list).Clear());
            Equal(10, store.GetById("G-1")!.Quantity);
            store.Update(read);
            read.RegisterArrival(400);
            Equal(210, store.GetById("G-1")!.Quantity);
        });
        Check($"{mode}: null and empty arguments are rejected", () =>
        {
            ICatalogStore store = MakeStore(mode);
            Throws<ArgumentNullException>(() => store.Add(null!));
            Throws<ArgumentNullException>(() => store.Update(null!));
            Throws<ArgumentNullException>(() => store.GetById(null!));
            Throws<ArgumentNullException>(() => store.Remove(null!));
            foreach (string id in new[] { "", " " })
            {
                Throws<ArgumentException>(() => store.GetById(id));
                Throws<ArgumentException>(() => store.Remove(id));
            }
        });
        Check($"{mode}: service operations and predicate search", () =>
        {
            var service = new CatalogService(MakeStore(mode));
            Goods item = service.Add(" sku ", " Кабель ", "м", 50, "note");
            service.Receive(item.Id, 25);
            service.Issue(item.Id, 10);
            Equal(65, service.Find(item.Id)!.Quantity);
            Equal("note", service.Find(item.Id)!.Note);
            Equal(1, service.Search(g => g.Unit == "м" && g.Quantity >= 60).Count);
            Equal(0, service.Search(g => g.Quantity > 65).Count);
            Throws<ArgumentNullException>(() => service.Search(null!));
            service.Search(g => { g.RegisterArrival(1); return true; });
            Equal(65, service.Find(item.Id)!.Quantity);
            Throws<NotSupportedException>(() => ((IList<Goods>)service.Search(_ => true)).Clear());
            True(service.Remove(item.Id));
            True(service.Find(item.Id) is null);
        });
        Check($"{mode}: service failures preserve state", () =>
        {
            var service = new CatalogService(MakeStore(mode));
            Goods item = service.Add("sku", "Name", "kg", 10);
            Throws<InvalidOperationException>(() => service.Receive("missing", 1));
            Throws<InvalidOperationException>(() => service.Issue("missing", 1));
            Throws<InvalidOperationException>(() => service.Issue(item.Id, 11));
            Throws<ArgumentOutOfRangeException>(() => service.Receive(item.Id, 0));
            Throws<ArgumentOutOfRangeException>(() => service.Issue(item.Id, -1));
            Throws<ArgumentException>(() => service.Add(" ", "Name", "kg", 0));
            Equal(10, service.Find(item.Id)!.Quantity);
            Equal(1, service.All().Count);
            service.Receive(item.Id, int.MaxValue - 10);
            Throws<InvalidOperationException>(() => service.Receive(item.Id, 1));
            Equal(int.MaxValue, service.Find(item.Id)!.Quantity);
            service.Issue(item.Id, int.MaxValue);
            Equal(0, service.Find(item.Id)!.Quantity);
        });
    }

    Check("Seed has 18 distinct, independent valid entities", () =>
    {
        var first = SampleData.Goods();
        var second = SampleData.Goods();
        Equal(18, first.Count);
        Equal(18, first.Select(g => g.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        var memory = new InMemoryCatalogStore(first);
        first[0].RegisterArrival(1);
        Equal(100, memory.GetById("G-001")!.Quantity);
        Equal(100, second[0].Quantity);
        Throws<InvalidOperationException>(() => new InMemoryCatalogStore([MakeGoods(), MakeGoods("g-1")]));
        Throws<ArgumentNullException>(() => new InMemoryCatalogStore([null!]));
    });
    Check("Null dependencies and invalid file paths are rejected", () =>
    {
        Throws<ArgumentNullException>(() => new CatalogService(null!));
        Throws<ArgumentNullException>(() => new CachingCatalogStore(null!));
        Throws<ArgumentNullException>(() => new FileCatalogStore(null!));
        Throws<ArgumentException>(() => new FileCatalogStore(" "));
    });
    Check("File writes Add, Update and Remove immediately as DTOs", () =>
    {
        string path = NewPath();
        var store = new FileCatalogStore(path);
        store.Add(MakeGoods());
        Equal(MakeGoods().ToDto(), new FileCatalogStore(path).GetById("G-1")!.ToDto());
        Goods item = store.GetById("G-1")!;
        item.RegisterArrival(5);
        store.Update(item);
        Equal(15, new FileCatalogStore(path).GetById("G-1")!.Quantity);
        var dtos = JsonSerializer.Deserialize<List<GoodsDto>>(File.ReadAllText(path))!;
        Equal(item.ToDto(), dtos.Single());
        store.Remove("g-1");
        Equal(0, new FileCatalogStore(path).List().Count);
        Equal("[]", File.ReadAllText(path));
    });
    Check("Missing file stays absent on read and creates parent folders on write", () =>
    {
        string path = Path.Combine(root, "nested", "data", "catalog.json");
        var store = new FileCatalogStore(path);
        Equal(0, store.List().Count);
        True(!File.Exists(path));
        True(!store.Remove("missing"));
        True(!File.Exists(path));
        store.Add(MakeGoods());
        True(File.Exists(path));
    });
    Check("File loads lazily once and a new store sees later disk changes", () =>
    {
        string path = NewPath();
        var store = new FileCatalogStore(path);
        File.WriteAllText(path, JsonSerializer.Serialize(new[] { MakeGoods().ToDto() }));
        Equal(1, store.List().Count);
        File.WriteAllText(path, "[]");
        Equal(1, store.List().Count);
        Equal(0, new FileCatalogStore(path).List().Count);
    });
    var invalidDocuments = new Dictionary<string, string>
    {
        ["malformed"] = "{broken",
        ["null root"] = "null",
        ["object root"] = "{}",
        ["null item"] = "[null]",
        ["missing properties"] = "[{}]",
        ["negative quantity"] = JsonSerializer.Serialize(new[] { MakeGoods().ToDto() with { Quantity = -1 } }),
        ["duplicate ids"] = JsonSerializer.Serialize(new[] { MakeGoods().ToDto(), MakeGoods("g-1").ToDto() })
    };
    foreach ((string label, string json) in invalidDocuments)
        Check($"Invalid JSON ({label}) is preserved, with no partially loaded cache", () =>
        {
            string path = NewPath();
            File.WriteAllText(path, json);
            var store = new FileCatalogStore(path);
            Throws<InvalidDataException>(() => store.List());
            Throws<InvalidDataException>(() => store.Add(MakeGoods("G-2")));
            Equal(json, File.ReadAllText(path));
            File.WriteAllText(path, "[]");
            Equal(0, store.List().Count);
        });
    foreach (string operation in new[] { "Add", "Update", "Remove" })
        Check($"Failed file {operation} does not publish a changed cache", () =>
        {
            string path = NewPath();
            var store = new FileCatalogStore(path);
            store.Add(MakeGoods());
            File.Delete(path);
            Directory.CreateDirectory(path); // A directory cannot be replaced with a JSON file.
            try
            {
                ThrowsIo(() =>
                {
                    if (operation == "Add") store.Add(MakeGoods("G-2"));
                    else if (operation == "Update") store.Update(MakeGoods(quantity: 99));
                    else store.Remove("G-1");
                });
                Equal(1, store.List().Count);
                Equal(10, store.GetById("G-1")!.Quantity);
                Equal(0, Directory.GetFiles(root, ".*.tmp").Length);
            }
            finally { Directory.Delete(path); }
        });
    if (OperatingSystem.IsWindows())
        Check("Locked destination preserves existing JSON and cached stock", () =>
        {
            string path = NewPath();
            var store = new FileCatalogStore(path);
            store.Add(MakeGoods());
            string before = File.ReadAllText(path);
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                ThrowsIo(() => store.Update(MakeGoods(quantity: 88)));
            Equal(before, File.ReadAllText(path));
            Equal(10, store.GetById("G-1")!.Quantity);
        });

    Check("Decorator caches List/GetById and invalidates after each mutation", () =>
    {
        var counted = new CountingStore(new InMemoryCatalogStore([MakeGoods()]));
        var cached = new CachingCatalogStore(counted);
        Equal(0, counted.ListCalls);
        cached.List();
        cached.List();
        cached.GetById("G-1");
        cached.GetById("missing");
        Equal(1, counted.ListCalls);
        Equal(0, counted.GetCalls);
        cached.Add(MakeGoods("G-2"));
        Equal(2, cached.List().Count);
        Equal(2, counted.ListCalls);
        cached.Update(MakeGoods(quantity: 20));
        Equal(20, cached.GetById("G-1")!.Quantity);
        Equal(3, counted.ListCalls);
        cached.Remove("G-1");
        True(cached.GetById("G-1") is null);
        Equal(4, counted.ListCalls);
        Throws<InvalidOperationException>(() => cached.Add(MakeGoods("G-2")));
        Equal(1, cached.List().Count);
        Equal(4, counted.ListCalls);
    });
    Check("Service passes successful changes through Update only", () =>
    {
        var counted = new CountingStore(new InMemoryCatalogStore());
        var service = new CatalogService(counted);
        Goods item = service.Add("SKU", "Name", "kg", 1);
        service.Receive(item.Id, 2);
        service.Issue(item.Id, 1);
        Equal(2, counted.UpdateCalls);
        Throws<InvalidOperationException>(() => service.Issue(item.Id, 100));
        Equal(2, counted.UpdateCalls);
    });
    Check("Decorator preserves cached data when underlying write fails", () =>
    {
        var counted = new CountingStore(new InMemoryCatalogStore([MakeGoods()])) { FailUpdate = true };
        var service = new CatalogService(new CachingCatalogStore(counted));
        Equal(10, service.Find("G-1")!.Quantity);
        Throws<IOException>(() => service.Receive("G-1", 5));
        Equal(10, service.Find("G-1")!.Quantity);
        Equal(1, counted.ListCalls);
    });
    Check("Factory creates all four combinations through the common contract", () =>
    {
        foreach (bool file in new[] { false, true })
        foreach (bool cache in new[] { false, true })
        {
            var arguments = new List<string> { "--factory" };
            if (file) arguments.AddRange(["--file", "--path", NewPath()]);
            if (cache) arguments.Add("--cache");
            ICatalogStore store = StoreFactory.Create(arguments.ToArray());
            True(cache ? store is CachingCatalogStore : file ? store is FileCatalogStore : store is InMemoryCatalogStore);
            int initial = file ? 0 : 18;
            Equal(initial, store.List().Count);
            new CatalogService(store).Add("X", "Factory", "kg", 1);
            Equal(initial + 1, store.List().Count);
        }
    });
    Check("CLI options validate input and resolve default and explicit paths", () =>
    {
        StoreOptions defaults = StoreOptions.Parse([]);
        True(!defaults.UseFile && !defaults.UseCache && !defaults.UseFactory && !defaults.ListOnly);
        Equal(Path.Combine(AppContext.BaseDirectory, "data", "catalog.json"), defaults.DataPath);
        var selected = StoreOptions.Parse(["--file", "--path", "data/catalog.json", "--cache", "--factory", "--list-only"]);
        True(selected.UseFile && selected.UseCache && selected.UseFactory && selected.ListOnly);
        Equal(Path.GetFullPath("data/catalog.json"), selected.DataPath);
        Throws<ArgumentNullException>(() => StoreOptions.Parse(null!));
        foreach (string[] arguments in new string[][]
        {
            ["--unknown"], ["--path"], ["--file", "--path", ""], ["--path", "file.json"],
            ["--file", "--path", "--cache"], ["--file", "--path", "one.json", "--path", "two.json"]
        }) Throws<ArgumentException>(() => StoreOptions.Parse(arguments));
    });
}
finally { Directory.Delete(root, recursive: true); }

Console.WriteLine($"Service checks: {passed} passed, {failed} failed");
return failed == 0 ? 0 : 1;

void Check(string name, Action action)
{
    try { action(); passed++; Console.WriteLine($"PASS {name}"); }
    catch (Exception ex) { failed++; Console.WriteLine($"FAIL {name}: {ex.GetType().Name}: {ex.Message}"); }
}
string NewPath() => Path.Combine(root, $"{Guid.NewGuid():N}.json");
ICatalogStore MakeStore(string mode)
{
    ICatalogStore store = mode.EndsWith("file") ? new FileCatalogStore(NewPath()) : new InMemoryCatalogStore();
    return mode.StartsWith("cached") ? new CachingCatalogStore(store) : store;
}
static Goods MakeGoods(string id = "G-1", int quantity = 10) => Goods.Create(id, "SKU", "Цемент", "кг", quantity, "Примітка");
static void True(bool condition)
{
    if (!condition) throw new InvalidOperationException("Condition is false");
}
static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"Expected {expected}, got {actual}");
}
static void Throws<TException>(Action action) where TException : Exception
{
    try { action(); }
    catch (Exception ex)
    {
        if (ex.GetType() != typeof(TException))
            throw new InvalidOperationException($"Expected {typeof(TException).Name}, got {ex.GetType().Name}");
        True(!string.IsNullOrWhiteSpace(ex.Message));
        return;
    }
    throw new InvalidOperationException($"Expected {typeof(TException).Name}, no exception thrown");
}
static void ThrowsIo(Action action)
{
    try { action(); }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return; }
    throw new InvalidOperationException("Expected an I/O failure");
}

sealed class CountingStore(ICatalogStore inner) : ICatalogStore
{
    public int ListCalls { get; private set; }
    public int GetCalls { get; private set; }
    public int UpdateCalls { get; private set; }
    public bool FailUpdate { get; init; }
    public IReadOnlyList<Goods> List() { ListCalls++; return inner.List(); }
    public Goods? GetById(string id) { GetCalls++; return inner.GetById(id); }
    public void Add(Goods item) => inner.Add(item);
    public void Update(Goods item)
    {
        UpdateCalls++;
        if (FailUpdate) throw new IOException("Simulated write failure");
        inner.Update(item);
    }
    public bool Remove(string id) => inner.Remove(id);
}
