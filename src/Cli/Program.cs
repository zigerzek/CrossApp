using Cli.Composition;
using Cli.Notifications;
using Core;
using Core.Abstractions;
using Core.Domain;
using Core.Events;
using Core.Import;
using Core.Services;
using Core.Storage;

Console.OutputEncoding = System.Text.Encoding.UTF8;
if (args is ["--help"])
{
    Console.WriteLine("Лабораторна 6: [--file] [--path <файл.json>] [--cache] [--factory] [--list-only]");
    Console.WriteLine("Без --file: 18 початкових товарів у пам'яті. --file: постійне JSON-сховище.");
    Console.WriteLine("--cache: декоратор; --factory: фабрика; --list-only: читання без змін і журналу.");
    Console.WriteLine("Звичайний запуск показує події, відписку, відмови та обидва додаткові завдання.");
    Console.WriteLine("Журнали: logs/app.log та logs/sink.log поруч із виконуваним файлом.");
    return 0;
}

try
{
    StoreOptions options = StoreOptions.Parse(args);
    ICatalogStore store;
    if (options.UseFactory)
        store = StoreFactory.Create(args);
    else
    {
        store = options.UseFile
            ? new FileCatalogStore(options.DataPath)
            : new InMemoryCatalogStore(SampleData.Goods());
        if (options.UseCache) store = new CachingCatalogStore(store);
    }
    var service = new CatalogService(store);
    Console.WriteLine("Лабораторна робота 6 — доменні події Warehouse");
    Console.WriteLine($"Сховище: {store.GetType().Name}");
    Console.WriteLine($"Створення: {(options.UseFactory ? "StoreFactory.Create(args)" : "Program.cs (ручний DI)")}");
    if (options.UseFile) Console.WriteLine($"Файл: {options.DataPath}");
    Console.WriteLine($"На початку: {service.All().Count} товарів");
    if (options.ListOnly)
    {
        PrintGoods(service.All());
        Console.WriteLine("Режим читання: дані не змінено.");
        return 0;
    }

    string logPath = Path.Combine(AppContext.BaseDirectory, "logs", "app.log");
    var consoleSink = new ConsoleSink();
    var fileSink = new FileSink(logPath);
    int changes = 0, loggedChanges = 0;
    void OnCatalogChanged(object? sender, CatalogChangedEventArgs e)
    {
        Console.WriteLine($"  [1] Консоль і файл; лічильник до другого обробника: {changes}");
        consoleSink.Notify(e);
        fileSink.Notify(e);
        loggedChanges++;
    }
    EventHandler<CatalogChangedEventArgs> counter = (_, _) =>
    {
        changes++;
        Console.WriteLine($"  [2] Лямбда-лічильник: {changes}");
    };
    service.Changed += OnCatalogChanged;
    service.Changed += counter;
    bool passed;
    try
    {
        Console.WriteLine($"Журнал подій: {logPath}");
        Console.WriteLine("\n=== Успішні операції: порядок підписників [1], [2] ===");
        Goods created = service.Add("DEMO-006", "Кабель для демонстрації подій", "м", 50, "Лабораторна 6");
        service.Receive(created.Id, 25);
        service.Issue(created.Id, 10);
        Goods temporary = service.Add("TEMP-006", "Тимчасові рукавиці", "пара", 3);
        bool removed = service.Remove(temporary.Id);
        Console.WriteLine($"Подій отримано: {changes}; повідомлень першого обробника: {loggedChanges}");
        passed = removed && changes == 5 && loggedChanges == 5 && service.Find(created.Id)?.Quantity == 65;

        Console.WriteLine("\n=== Відмови: повідомлень про зміну немає ===");
        int beforeFailure = changes;
        passed &= ExpectFailure("Від'ємний прихід", () => service.Receive(created.Id, -5));
        passed &= ExpectFailure("Відсутній id", () => service.Receive("__missing_lab06__", 1));
        passed &= ExpectFailure("Видача понад залишок", () => service.Issue(created.Id, 1000));
        bool removedAgain = service.Remove(temporary.Id);
        Console.WriteLine($"Повторне видалення: {removedAgain}; нових подій: {changes - beforeFailure}");
        Console.WriteLine($"Залишок після відмов: {service.Find(created.Id)?.Quantity}");
        passed &= !removedAgain && changes == beforeFailure && service.Find(created.Id)?.Quantity == 65;

        Console.WriteLine("\n=== Func: FindAll(p => p.Quantity < 10) ===");
        PrintGoods(service.FindAll(p => p.Quantity < 10));
        passed &= changes == beforeFailure;

        Console.WriteLine("\n=== Відписка першого обробника ===");
        service.Changed -= OnCatalogChanged;
        service.Receive(created.Id, 5);
        Console.WriteLine($"Залишок: {service.Find(created.Id)?.Quantity}; подій отримано: {changes}");
        Console.WriteLine($"Повідомлень першого обробника: {loggedChanges}; нового рядка у журналі немає.");
        passed &= service.Find(created.Id)?.Quantity == 70 && changes == 6 && loggedChanges == 5;

        Console.WriteLine("\n=== Відписка лямбди: жодного підписника ===");
        service.Changed -= counter;
        service.Issue(created.Id, 1);
        Console.WriteLine($"Залишок: {service.Find(created.Id)?.Quantity}; лічильник залишився: {changes}");
        passed &= service.Find(created.Id)?.Quantity == 69 && changes == 6 && loggedChanges == 5;
    }
    finally
    {
        service.Changed -= OnCatalogChanged;
        service.Changed -= counter;
    }
    passed &= consoleSink.FailureCount == 0 && fileSink.FailureCount == 0;
    passed &= DemonstrateSinks();
    passed &= DemonstrateImport();
    Console.WriteLine(passed ? "\nДемонстрацію лабораторної 6 завершено успішно." : "\nДемонстрація завершилась з помилками; перевірте повідомлення вище.");
    return passed ? 0 : 1;
}
catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException)
{
    Console.Error.WriteLine($"Помилка: {ex.Message}");
    return 1;
}

static bool DemonstrateSinks()
{
    Console.WriteLine("\n=== Додаткове 1: INotificationSink через конструктор ===");
    var console = new ConsoleSink();
    var file = new FileSink(Path.Combine(AppContext.BaseDirectory, "logs", "sink.log"));
    foreach (INotificationSink sink in new INotificationSink[] { console, file })
    {
        Console.WriteLine($"Новий сервіс із {sink.GetType().Name}, без підписників Changed:");
        var service = new CatalogService(new InMemoryCatalogStore(), sink);
        Goods item = service.Add("SINK-006", "Товар для перевірки DI", "шт", 5);
        service.Receive(item.Id, 2);
        service.Issue(item.Id, 1);
        service.Remove(item.Id);
        Console.WriteLine($"Операції виконано, товар видалено: {service.Find(item.Id) is null}");
    }
    Console.WriteLine($"Окремий журнал прямого DI: {file.LogPath}");
    Console.WriteLine("Подія дозволяє += / -=; sink є залежністю сервісу, переданою через конструктор.");
    return console.FailureCount == 0 && file.FailureCount == 0;
}

static bool DemonstrateImport()
{
    Console.WriteLine("\n=== Додаткове 2: Action<string> у методі імпорту ===");
    int skipped = 0;
    string path = Path.Combine(AppContext.BaseDirectory, "data", "lab06.csv");
    var result = GoodsCsvImporter.Load(path, message =>
    {
        skipped++;
        Console.WriteLine($"Пропущено: {message}");
    });
    Console.WriteLine($"Імпортовано DTO: {result.Items.Count}; помилок: {result.Errors.Count}; викликів Action: {skipped}");
    Console.WriteLine("Помилки також збережено в ImportResult.Errors; імпортер не залежить від консолі.");
    return result.Items.Count == 3 && result.Errors.Count == 3 && skipped == 3;
}

static void PrintGoods(IReadOnlyList<Goods> items)
{
    foreach (Goods item in items) Console.WriteLine($"  {item}");
    if (items.Count == 0) Console.WriteLine("  За цією умовою товарів немає.");
}

static bool ExpectFailure(string title, Action action)
{
    try
    {
        action();
        Console.WriteLine($"ПОМИЛКА: {title} — виняток не виник.");
        return false;
    }
    catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
    {
        Console.WriteLine($"{title}: {ex.GetType().Name} — {ex.Message}");
        return true;
    }
}
