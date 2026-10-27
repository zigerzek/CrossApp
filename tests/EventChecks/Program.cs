using Cli.Notifications;
using Core.Abstractions;
using Core.Domain;
using Core.Events;
using Core.Import;
using Core.Services;
using Core.Storage;

int passed = 0, failed = 0;
string root = Path.Combine(Path.GetTempPath(), $"crossapp-lab06-{Guid.NewGuid():N}");
Directory.CreateDirectory(root);
try
{
    foreach (string mode in new[] { "memory", "file", "cached-memory", "cached-file" })
    {
        Check($"{mode}: one event per committed change, correct sender and payload", () =>
        {
            ICatalogStore store = MakeStore(mode);
            var service = new CatalogService(store);
            var received = new List<CatalogChangedEventArgs>();
            DateTimeOffset start = DateTimeOffset.Now;
            service.Changed += (sender, change) =>
            {
                True(ReferenceEquals(service, sender));
                True(change.At >= start && change.At <= DateTimeOffset.Now);
                // The persisted state must already be visible inside the subscriber.
                if (change.Kind == CatalogChangeKind.Removed) True(store.GetById(change.Id) is null);
                else Equal(change.QuantityAfter, store.GetById(change.Id)!.Quantity);
                received.Add(change);
            };
            Goods item = service.Add("SKU", "Кабель", "м", 50);
            service.Receive(item.Id, 25);
            service.Issue(item.Id, 10);
            True(service.Remove(item.Id));
            True(!service.Remove(item.Id));
            Equal(4, received.Count);
            Equal((CatalogChangeKind.Added, 50, 50), Payload(received[0]));
            Equal((CatalogChangeKind.Received, 25, 75), Payload(received[1]));
            Equal((CatalogChangeKind.Issued, -10, 65), Payload(received[2]));
            Equal((CatalogChangeKind.Removed, -65, 0), Payload(received[3]));
            True(received.All(e => e.Id == item.Id));
            Equal(50, received[0].QuantityAfter); // Snapshot, not a live Goods reference.
        });
        Check($"{mode}: invariant failures and missing IDs never notify", () =>
        {
            var sink = new RecordingSink();
            var service = new CatalogService(MakeStore(mode), sink);
            Goods item = service.Add("SKU", "Name", "шт", 10);
            int count = 0;
            service.Changed += (_, _) => count++;
            Throws<ArgumentException>(() => service.Add(" ", "Name", "шт", 0));
            Throws<ArgumentOutOfRangeException>(() => service.Add("SKU", "Name", "шт", -1));
            foreach (int amount in new[] { 0, -1, int.MinValue })
            {
                Throws<ArgumentOutOfRangeException>(() => service.Receive(item.Id, amount));
                Throws<ArgumentOutOfRangeException>(() => service.Issue(item.Id, amount));
            }
            Throws<InvalidOperationException>(() => service.Issue(item.Id, 11));
            Throws<InvalidOperationException>(() => service.Receive(item.Id, int.MaxValue));
            Throws<InvalidOperationException>(() => service.Receive("missing", 1));
            Throws<InvalidOperationException>(() => service.Issue("missing", 1));
            True(!service.Remove("missing"));
            Equal(0, count);
            Equal(1, sink.Changes.Count);
            Equal(10, service.Find(item.Id)!.Quantity);
        });
        Check($"{mode}: queries never notify, predicates cannot modify storage", () =>
        {
            var service = new CatalogService(MakeStore(mode));
            Goods item = service.Add("SKU", "Name", "шт", 5);
            service.Add("OTHER", "Name", "шт", 20);
            int count = 0;
            service.Changed += (_, _) => count++;
            Equal(2, service.All().Count);
            Equal(5, service.Find(item.Id)!.Quantity);
            Equal(1, service.FindAll(p => p.Quantity < 10).Count);
            Equal(0, service.FindAll(_ => false).Count);
            Throws<ArgumentNullException>(() => service.FindAll(null!));
            Throws<NotSupportedException>(() => ((IList<Goods>)service.FindAll(_ => true)).Clear());
            service.FindAll(p => { p.RegisterArrival(100); return true; });
            Equal(5, service.Find(item.Id)!.Quantity);
            Equal(0, count);
        });
        Check($"{mode}: no subscribers is safe, zero stock and int boundaries", () =>
        {
            var service = new CatalogService(MakeStore(mode));
            Goods item = service.Add("SKU", "Name", "шт", 0);
            service.Receive(item.Id, int.MaxValue);
            var received = new List<CatalogChangedEventArgs>();
            service.Changed += (_, e) => received.Add(e);
            service.Issue(item.Id, int.MaxValue);
            True(service.Remove(item.Id));
            Equal((CatalogChangeKind.Issued, -int.MaxValue, 0), Payload(received[0]));
            Equal((CatalogChangeKind.Removed, 0, 0), Payload(received[1]));
        });
    }

    Check("Subscribers run in order; stored lambda and local handler can be removed", () =>
    {
        var service = new CatalogService(new InMemoryCatalogStore());
        var order = new List<string>();
        int changes = 0;
        void First(object? sender, CatalogChangedEventArgs e) => order.Add($"first:{changes}");
        EventHandler<CatalogChangedEventArgs> counter = (_, _) => { changes++; order.Add($"second:{changes}"); };
        service.Changed += First;
        service.Changed += counter;
        Goods item = service.Add("SKU", "Name", "шт", 50);
        Equal("first:0,second:1", string.Join(',', order));
        service.Changed -= First;
        service.Receive(item.Id, 5);
        Equal("first:0,second:1,second:2", string.Join(',', order));
        service.Changed -= counter;
        service.Receive(item.Id, 5);
        Equal(2, changes);
        Equal(60, service.Find(item.Id)!.Quantity);
    });

    Check("Uncaught subscriber exception stops later handlers; commit already happened", () =>
    {
        var store = new InMemoryCatalogStore();
        var service = new CatalogService(store);
        int later = 0;
        service.Changed += (_, _) => throw new ApplicationException("subscriber failure");
        service.Changed += (_, _) => later++;
        Throws<ApplicationException>(() => service.Add("SKU", "Name", "шт", 1));
        Equal(0, later);
        Equal(1, store.List().Count);
    });

    foreach (string operation in new[] { "Add", "Update", "Remove" })
        Check($"Failed store {operation} never publishes event or sink notification", () =>
        {
            var store = new RejectingStore();
            var sink = new RecordingSink();
            var service = new CatalogService(store, sink);
            Goods item = service.Add("SKU", "Name", "шт", 10);
            int count = 0;
            service.Changed += (_, _) => count++;
            store.Reject = operation;
            if (operation == "Add") Throws<IOException>(() => service.Add("NEW", "Name", "шт", 0));
            if (operation == "Update")
            {
                Throws<IOException>(() => service.Receive(item.Id, 1));
                Throws<IOException>(() => service.Issue(item.Id, 1));
            }
            if (operation == "Remove") Throws<IOException>(() => service.Remove(item.Id));
            Equal(0, count);
            Equal(1, sink.Changes.Count);
            Equal(10, store.GetById(item.Id)!.Quantity);
            Equal(1, store.List().Count);
        });

    Check("Store Remove returning false never publishes Removed", () =>
    {
        var store = new RejectingStore();
        var service = new CatalogService(store);
        Goods item = service.Add("SKU", "Name", "шт", 1);
        int changes = 0;
        service.Changed += (_, _) => changes++;
        store.RemoveReturnsFalse = true;
        True(!service.Remove(item.Id));
        Equal(0, changes);
    });

    Check("Actual file write failure does not emit an event or change cached stock", () =>
    {
        string path = Path.Combine(root, Guid.NewGuid().ToString("N") + ".json");
        var service = new CatalogService(new FileCatalogStore(path));
        Goods item = service.Add("SKU", "Name", "шт", 10);
        File.Delete(path); // Only the test-owned temporary file.
        Directory.CreateDirectory(path); // Destination is now a directory, so commit must fail.
        int changes = 0;
        service.Changed += (_, _) => changes++;
        ThrowsIo(() => service.Receive(item.Id, 1));
        Equal(0, changes);
        Equal(10, service.Find(item.Id)!.Quantity);
    });

    Check("EventArgs are read-only and the service exposes an event, not a public delegate field", () =>
    {
        True(typeof(EventArgs).IsAssignableFrom(typeof(CatalogChangedEventArgs)));
        True(typeof(CatalogChangedEventArgs).GetProperties().All(p => p.SetMethod is null));
        True(typeof(CatalogService).GetEvent("Changed") is not null);
        True(typeof(CatalogService).GetField("Changed") is null);
    });

    Check("Constructor sink receives each successful change without event subscribers", () =>
    {
        var sink = new RecordingSink();
        var service = new CatalogService(new InMemoryCatalogStore(), sink);
        Goods item = service.Add("SKU", "Name", "шт", 5);
        service.Receive(item.Id, 2);
        service.Issue(item.Id, 1);
        service.Remove(item.Id);
        Equal("Added,Received,Issued,Removed", string.Join(',', sink.Changes.Select(e => e.Kind)));
        Equal((CatalogChangeKind.Removed, -6, 0), Payload(sink.Changes[^1]));
    });
    Check("Sink and event receive the same immutable payload after commit", () =>
    {
        var sink = new RecordingSink();
        var service = new CatalogService(new InMemoryCatalogStore(), sink);
        CatalogChangedEventArgs? observed = null;
        service.Changed += (_, e) => observed = e;
        service.Add("SKU", "Name", "шт", 1);
        True(ReferenceEquals(observed, sink.Changes.Single()));
    });
    Check("ConsoleSink works through constructor injection", () =>
    {
        using var output = new StringWriter();
        var sink = new ConsoleSink(output);
        var service = new CatalogService(new InMemoryCatalogStore(), sink);
        Goods item = service.Add("SKU", "Name", "шт", 4);
        service.Issue(item.Id, 1);
        True(output.ToString().Contains("Added") && output.ToString().Contains("Issued"));
        True(output.ToString().Contains("залишок 3"));
        Equal(0, sink.FailureCount);
    });
    Check("FileSink creates directories and appends UTF-8 through constructor injection", () =>
    {
        string path = Path.Combine(root, "new", "logs", "app.log");
        var sink = new FileSink(path);
        var service = new CatalogService(new InMemoryCatalogStore(), sink);
        Goods item = service.Add("SKU", "Name", "шт", 4);
        service.Issue(item.Id, 1);
        var second = new FileSink(path);
        second.Notify(new CatalogChangedEventArgs("other", CatalogChangeKind.Added, 0, 0));
        string[] lines = File.ReadAllLines(path);
        Equal(3, lines.Length);
        True(lines[0].Contains(item.Id) && lines[0].Contains("+4"));
        True(lines[1].Contains("-1") && lines[1].Contains("залишок 3"));
        True(lines[2].Contains("+0"));
        Equal(0, sink.FailureCount);
        Throws<ArgumentException>(() => new FileSink(" "));
        Throws<ArgumentNullException>(() => sink.Notify(null!));
    });
    Check("Unsubscribed file handler writes nothing; lambda still receives the event", () =>
    {
        string path = Path.Combine(root, "unsubscribe.log");
        var sink = new FileSink(path);
        var service = new CatalogService(new InMemoryCatalogStore());
        int changes = 0;
        void Handler(object? sender, CatalogChangedEventArgs e) => sink.Notify(e);
        service.Changed += Handler;
        service.Changed += (_, _) => changes++;
        Goods item = service.Add("SKU", "Name", "шт", 50);
        byte[] before = File.ReadAllBytes(path);
        service.Changed -= Handler;
        service.Receive(item.Id, 5);
        True(before.SequenceEqual(File.ReadAllBytes(path)));
        Equal(2, changes);
        Equal(55, service.Find(item.Id)!.Quantity);
    });
    Check("FileSink handles failed IO inside subscriber so the second subscriber still runs", () =>
    {
        string parent = Path.Combine(root, "a-file-not-directory");
        File.WriteAllText(parent, "keep");
        var errors = new List<string>();
        var sink = new FileSink(Path.Combine(parent, "app.log"), errors.Add);
        var service = new CatalogService(new InMemoryCatalogStore());
        int changes = 0;
        service.Changed += (_, e) => sink.Notify(e);
        service.Changed += (_, _) => changes++;
        Goods item = service.Add("SKU", "Name", "шт", 1);
        Equal(1, changes);
        Equal(1, sink.FailureCount);
        Equal(1, errors.Count);
        Equal(1, service.Find(item.Id)!.Quantity);
        Equal("keep", File.ReadAllText(parent));
    });
    Check("Failed console writer is reported without aborting the service", () =>
    {
        var errors = new List<string>();
        var sink = new ConsoleSink(new BrokenWriter(), errors.Add);
        var service = new CatalogService(new InMemoryCatalogStore(), sink);
        Goods item = service.Add("SKU", "Name", "шт", 1);
        Equal(1, sink.FailureCount);
        Equal(1, errors.Count);
        Equal(1, service.Find(item.Id)!.Quantity);
    });

    Check("Import Action reports each malformed physical line once and retains errors", () =>
    {
        string path = Path.Combine(root, "import.csv");
        File.WriteAllText(path, "id;sku;name;unit;quantity\nG1;SKU;Кабель;м;5\n# comment\n\nshort\nG2;SKU;Name;шт;bad\nG3;;Name;шт;2\nG4;SKU;Окуляри;шт;0\n");
        var notices = new List<string>();
        var result = GoodsCsvImporter.Load(path, notices.Add);
        Equal(2, result.Items.Count);
        Equal(3, result.Errors.Count);
        True(result.Errors.SequenceEqual(notices));
        True(notices[0].StartsWith("рядок 5:") && notices[1].StartsWith("рядок 6:") && notices[2].StartsWith("рядок 7:"));
        True(result.SourceLineNumbers!.SequenceEqual(new[] { 2, 8 }));
        var withoutCallback = GoodsCsvImporter.Load(path);
        True(withoutCallback.Errors.SequenceEqual(result.Errors));
        True(withoutCallback.Items.SequenceEqual(result.Items));
    });
    Check("Empty and valid CSV do not invoke callback; missing file is not a skipped row", () =>
    {
        string path = Path.Combine(root, "valid.csv");
        File.WriteAllText(path, "");
        int calls = 0;
        Equal(0, GoodsCsvImporter.Load(path, _ => calls++).Items.Count);
        File.WriteAllText(path, "id;sku;name;unit;quantity\nG;SKU;Name;шт;0\n");
        Equal(1, GoodsCsvImporter.Load(path, _ => calls++).Items.Count);
        Throws<FileNotFoundException>(() => GoodsCsvImporter.Load(path + ".missing", _ => calls++));
        Equal(0, calls);
    });
    Check("Importer propagates callback failures rather than silently losing diagnostics", () =>
    {
        string path = Path.Combine(root, "callback.csv");
        File.WriteAllText(path, "broken");
        Throws<ApplicationException>(() => GoodsCsvImporter.Load(path, _ => throw new ApplicationException("callback")));
    });
}
finally
{
    // root is a unique, test-owned directory created above, never a user-provided path.
    Directory.Delete(root, true);
}
Console.WriteLine($"Event checks: {passed} passed, {failed} failed");
return failed == 0 ? 0 : 1;

ICatalogStore MakeStore(string mode)
{
    ICatalogStore store = mode.EndsWith("file", StringComparison.Ordinal)
        ? new FileCatalogStore(Path.Combine(root, Guid.NewGuid().ToString("N") + ".json"))
        : new InMemoryCatalogStore();
    return mode.StartsWith("cached", StringComparison.Ordinal) ? new CachingCatalogStore(store) : store;
}
void Check(string name, Action action)
{
    try { action(); passed++; Console.WriteLine($"PASS {name}"); }
    catch (Exception ex) { failed++; Console.WriteLine($"FAIL {name}: {ex.Message}"); }
}
static (CatalogChangeKind, int, int) Payload(CatalogChangedEventArgs e) => (e.Kind, e.Delta, e.QuantityAfter);
static void True(bool condition) { if (!condition) throw new Exception("Assertion failed"); }
static void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}, got {actual}"); }
static void Throws<T>(Action action) where T : Exception
{
    try { action(); } catch (T) { return; }
    throw new Exception($"Expected {typeof(T).Name}");
}
static void ThrowsIo(Action action)
{
    try { action(); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return; }
    throw new Exception("Expected IO failure");
}
sealed class RecordingSink : INotificationSink
{
    public List<CatalogChangedEventArgs> Changes { get; } = [];
    public void Notify(CatalogChangedEventArgs change) => Changes.Add(change);
}
sealed class BrokenWriter : StringWriter
{
    public override void WriteLine(string? value) => throw new IOException("output unavailable");
}
sealed class RejectingStore : ICatalogStore
{
    private readonly InMemoryCatalogStore _inner = new();
    public string? Reject { get; set; }
    public bool RemoveReturnsFalse { get; set; }
    public IReadOnlyList<Goods> List() => _inner.List();
    public Goods? GetById(string id) => _inner.GetById(id);
    public void Add(Goods item) { Fail("Add"); _inner.Add(item); }
    public void Update(Goods item) { Fail("Update"); _inner.Update(item); }
    public bool Remove(string id) { Fail("Remove"); return !RemoveReturnsFalse && _inner.Remove(id); }
    private void Fail(string operation) { if (Reject == operation) throw new IOException("write failed"); }
}
