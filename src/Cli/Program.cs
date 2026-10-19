using Cli.Composition;
using Core;
using Core.Abstractions;
using Core.Domain;
using Core.Services;
using Core.Storage;

Console.OutputEncoding = System.Text.Encoding.UTF8;
if (args is ["--help"])
{
    Console.WriteLine("Лабораторна 5: [--file] [--path <файл.json>] [--cache] [--factory] [--list-only]");
    Console.WriteLine("Без --file: 18 початкових товарів у пам'яті. --file: постійне JSON-сховище.");
    Console.WriteLine("--cache: декоратор; --factory: фабрика; --list-only: читання без змін.");
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
        // Основний варіант ручного DI: конкретні залежності створюються тут.
        store = options.UseFile
            ? new FileCatalogStore(options.DataPath)
            : new InMemoryCatalogStore(SampleData.Goods());
        if (options.UseCache) store = new CachingCatalogStore(store);
    }
    var service = new CatalogService(store);

    Console.WriteLine("Лабораторна робота 5 — сервісний шар Warehouse");
    Console.WriteLine($"Сховище: {store.GetType().Name}");
    Console.WriteLine($"Створення: {(options.UseFactory ? "StoreFactory.Create(args)" : "Program.cs (ручний DI)")}");
    if (options.UseFile) Console.WriteLine($"Файл: {options.DataPath}");
    if (options.UseCache) Console.WriteLine($"Декоратор над: {(options.UseFile ? "файловим сховищем" : "сховищем у пам'яті")}");
    Console.WriteLine($"На початку: {service.All().Count} товарів");

    if (options.ListOnly)
    {
        PrintGoods(service.All());
        Console.WriteLine("Режим читання: дані не змінено.");
        return 0;
    }

    Console.WriteLine("\n=== Add / Receive / Issue / Find ===");
    Goods created = service.Add("DEMO-005", "Кабель для демонстрації", "м", 50, "Лабораторна 5");
    Console.WriteLine($"Додано: {created}");
    service.Receive(created.Id, 25);
    Console.WriteLine($"Після приходу 25: {service.Find(created.Id)}");
    service.Issue(created.Id, 10);
    Console.WriteLine($"Знайдено за id після видачі 10: {service.Find(created.Id)}");

    Console.WriteLine("\n=== Очікувані відмови ===");
    bool passed = ExpectFailure("Повторний id", () => store.Add(created));
    passed &= ExpectFailure("Відсутній id", () => service.Receive("__missing_lab05__", 1));
    passed &= ExpectFailure("Видача понад залишок", () => service.Issue(created.Id, 1000));
    passed &= service.Find(created.Id)?.Quantity == 65;
    Console.WriteLine($"Залишок після відмов: {service.Find(created.Id)?.Quantity}");

    Goods temporary = service.Add("TEMP-005", "Тимчасовий товар", "шт", 0);
    bool removed = service.Remove(temporary.Id);
    bool removedAgain = service.Remove(temporary.Id);
    passed &= removed && !removedAgain && service.Find(temporary.Id) is null;
    Console.WriteLine($"Remove: {removed}; повторне видалення: {removedAgain}");

    Console.WriteLine("\n=== Додаткове завдання: Search(Func<Goods, bool>) ===");
    IReadOnlyList<Goods> found = service.Search(item => item.Unit == "м" && item.Quantity >= 60);
    PrintGoods(found);
    passed &= found.Any(item => item.Id == created.Id);

    Console.WriteLine($"\n=== Повний список: {service.All().Count} товарів ===");
    PrintGoods(service.All());
    if (options.UseCache)
    {
        // Друге читання та пошук використовують кеш декоратора.
        passed &= service.All().Any(item => item.Id == created.Id) && service.Find(created.Id)?.Quantity == 65;
        Console.WriteLine("Декоратор: повторні List/Find виконано; кількість звернень перевіряють ServiceChecks.");
    }

    Console.WriteLine(passed ? "\nДемонстрацію лабораторної 5 завершено успішно." : "\nПомилка перевірки сценарію.");
    return passed ? 0 : 1;
}
catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException)
{
    Console.Error.WriteLine($"Помилка: {ex.Message}");
    return 1;
}

static void PrintGoods(IReadOnlyList<Goods> items)
{
    foreach (Goods item in items) Console.WriteLine($"  {item}");
}

static bool ExpectFailure(string title, Action action)
{
    try
    {
        action();
        Console.WriteLine($"ПОМИЛКА: {title} — виняток не виник.");
        return false;
    }
    catch (InvalidOperationException ex)
    {
        Console.WriteLine($"{title}: {ex.Message}");
        return true;
    }
}
