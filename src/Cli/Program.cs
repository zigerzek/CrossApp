using Cli.Composition;
using Core;
using Core.Abstractions;
using Core.Domain;
using Core.Reports;
using Core.Services;
using Core.Storage;

Console.OutputEncoding = System.Text.Encoding.UTF8;
if (args is ["--help"])
{
    PrintHelp();
    return 0;
}

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

bool useStream = false;
bool immediateCancel = false;
TimeSpan? timeout = null;
bool showProgress = false;

var filteredArgs = new List<string>();
for (int i = 0; i < args.Length; i++)
{
    string arg = args[i];
    if (arg == "--stream")
    {
        useStream = true;
    }
    else if (arg == "--cancel")
    {
        immediateCancel = true;
    }
    else if (arg == "--progress")
    {
        showProgress = true;
    }
    else if (arg == "--timeout" && i + 1 < args.Length)
    {
        string tStr = args[++i];
        if (tStr.EndsWith("ms", StringComparison.OrdinalIgnoreCase) && int.TryParse(tStr[..^2], out int ms))
            timeout = TimeSpan.FromMilliseconds(ms);
        else if (tStr.EndsWith("s", StringComparison.OrdinalIgnoreCase) && double.TryParse(tStr[..^1], out double s))
            timeout = TimeSpan.FromSeconds(s);
        else if (double.TryParse(tStr, out double num))
            timeout = TimeSpan.FromSeconds(num);
    }
    else
    {
        filteredArgs.Add(arg);
    }
}

string[] activeArgs = filteredArgs.ToArray();
bool isFileImport = activeArgs.Contains("--file") || activeArgs.Contains("import");

if (isFileImport)
{
    (string _, string[] storeArguments) = SplitArguments(activeArgs);
    StoreOptions options = StoreOptions.Parse(storeArguments);
    string dataPath = options.DataPath;
    bool hasCustomPath = activeArgs.Contains("--path");
    if (!hasCustomPath && !File.Exists(dataPath) && File.Exists("data/catalog.json"))
        dataPath = Path.GetFullPath("data/catalog.json");

    if (timeout.HasValue)
        cts.CancelAfter(timeout.Value);
    if (immediateCancel)
        cts.Cancel();

    try
    {
        var store = new InMemoryCatalogStore();
        var service = new CatalogService(store);
        var importer = new JsonCatalogImporter();

        IProgress<int>? progress = showProgress
            ? new Progress<int>(percent => Console.Write($"\rПрогрес імпорту: {percent,3}%"))
            : null;

        int added = useStream
            ? await service.ImportStreamAsync(importer, dataPath, progress, cts.Token)
            : await service.ImportAsync(importer, dataPath, progress, cts.Token);

        if (showProgress)
            Console.WriteLine();

        Console.WriteLine($"Імпортовано записів: {added}");
        return 0;
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Імпорт скасовано.");
        return 0;
    }
    catch (DomainException ex)
    {
        Console.WriteLine($"Дані відхилено: {ex.Message}");
        return 1;
    }
    catch (FileNotFoundException)
    {
        Console.WriteLine($"Файл не знайдено: {dataPath}");
        return 1;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Помилка: {ex.Message}");
        return 1;
    }
}

try
{
    (string command, string[] storeArguments) = SplitArguments(activeArgs);
    StoreOptions options = StoreOptions.Parse(storeArguments);
    ICatalogStore store = options.UseFactory
        ? StoreFactory.Create(storeArguments)
        : CreateStore(options);
    var service = new CatalogService(store);
    IReadOnlyList<Goods> items = service.All();

    Console.WriteLine("Лабораторна робота 8 — Async/Await, Cancellation, Unit-тести для Warehouse");
    Console.WriteLine($"Сховище: {store.GetType().Name}; товарів: {items.Count}");

    if (options.ListOnly)
    {
        PrintGoods(items);
        Console.WriteLine("Режим читання: дані не змінено.");
        return 0;
    }

    switch (command)
    {
        case "report-top":
            Console.WriteLine("\n=== report-top: топ-5 за залишком ===");
            PrintStockLines(ReportService.TopByQuantity(items, 5));
            break;
        case "report-low":
            Console.WriteLine("\n=== report-low: залишок менший за 10 ===");
            PrintStockLines(ReportService.LowStock(items, 10));
            break;
        case "report-groups":
            Console.WriteLine("\n=== report-groups: групування за одиницею ===");
            PrintGroups(ReportService.ByUnit(items));
            break;
        case "report-sum":
            Console.WriteLine("\n=== report-sum: підсумки складу ===");
            PrintTotals(ReportService.Totals(items));
            break;
        case "report-batches":
            Console.WriteLine("\n=== Додаткове 1: Join товарів і партій ===");
            PrintBatchLines(ReportService.BatchesByGoods(items, SampleData.Batches()));
            break;
        case "report-top-batches":
            Console.WriteLine("\n=== Додаткове 2: TopBy<T> для партій ===");
            PrintBatchLines(ReportService.TopBatchesByQuantity(items, SampleData.Batches(), 3));
            break;
        case "report-benchmark":
            Console.WriteLine("\n=== Додаткове 4: Stopwatch на 100 000 записів ===");
            PrintBenchmark(ReportService.BenchmarkTopByQuantity());
            return 0;
        default:
            Console.Error.WriteLine($"Невідома команда: {command}.");
            PrintHelp();
            return 1;
    }

    DeferredExecutionResult deferred = ReportService.DemonstrateDeferredExecution(items, 10);
    Console.WriteLine("\n=== Відкладене виконання LINQ ===");
    Console.WriteLine($"До зміни джерела: {deferred.BeforeChange}");
    Console.WriteLine($"Після додавання товару: {deferred.AfterChange}");
    Console.WriteLine($"Знімок після очищення джерела: {deferred.SnapshotAfterClear}");
    return 0;
}
catch (OperationCanceledException)
{
    Console.WriteLine("Операцію скасовано.");
    return 0;
}
catch (DomainException ex)
{
    Console.WriteLine($"Дані відхилено: {ex.Message}");
    return 1;
}
catch (FileNotFoundException ex)
{
    Console.WriteLine($"Файл не знайдено: {ex.FileName}");
    return 1;
}
catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException)
{
    Console.Error.WriteLine($"Помилка: {ex.Message}");
    return 1;
}

static ICatalogStore CreateStore(StoreOptions options)
{
    ICatalogStore store = options.UseFile
        ? new FileCatalogStore(options.DataPath)
        : new InMemoryCatalogStore(SampleData.Goods());
    return options.UseCache ? new CachingCatalogStore(store) : store;
}

static (string Command, string[] StoreArguments) SplitArguments(string[] args)
{
    string? command = null;
    var storeArguments = new List<string>();
    for (int index = 0; index < args.Length; index++)
    {
        string argument = args[index];
        if (argument == "--path")
        {
            storeArguments.Add(argument);
            if (index + 1 < args.Length) storeArguments.Add(args[++index]);
            continue;
        }

        if (argument.StartsWith("--", StringComparison.Ordinal))
        {
            storeArguments.Add(argument);
            continue;
        }

        if (command is not null)
            throw new ArgumentException("Укажіть лише одну команду.");
        command = argument;
    }

    return (command ?? "report-top", storeArguments.ToArray());
}

static void PrintStockLines(IReadOnlyList<StockLine> lines)
{
    if (lines.Count == 0)
    {
        Console.WriteLine("  Немає рядків для цього звіту.");
        return;
    }

    foreach (StockLine line in lines)
        Console.WriteLine($"  {line.Sku,-10} {line.Name,-24} {line.Quantity,6} {line.Unit}");
}

static void PrintGroups(IReadOnlyList<GroupSummary> groups)
{
    if (groups.Count == 0)
    {
        Console.WriteLine("  Немає рядків для цього звіту.");
        return;
    }

    foreach (GroupSummary group in groups)
        Console.WriteLine($"  {group.Key,-6} позицій: {group.Count,3}; сумарно: {group.TotalQuantity,6}");
}

static void PrintTotals(StockTotals totals) =>
    Console.WriteLine($"  позицій {totals.Items}, всього {totals.TotalQuantity}, середнє {totals.AverageQuantity:F2}, макс {totals.MaxQuantity}");

static void PrintBatchLines(IReadOnlyList<BatchLine> lines)
{
    if (lines.Count == 0)
    {
        Console.WriteLine("  Немає партій, пов'язаних із товарами поточного сховища.");
        return;
    }

    foreach (BatchLine line in lines)
        Console.WriteLine($"  {line.BatchId,-6} {line.GoodsSku,-10} {line.GoodsName,-24} {line.BatchQuantity,5} {line.ReceivedDate:yyyy-MM-dd} {line.Status}");
}

static void PrintBenchmark(LinqBenchmark benchmark)
{
    Console.WriteLine($"  Записів: {benchmark.RecordCount}; top-N: {benchmark.ResultCountWithoutMiddleToList} / {benchmark.ResultCountWithMiddleToList}");
    Console.WriteLine($"  Без ToList у середині: {benchmark.WithoutMiddleToListMilliseconds} мс");
    Console.WriteLine($"  З ToList у середині:   {benchmark.WithMiddleToListMilliseconds} мс");
    Console.WriteLine("  Числа залежать від комп'ютера; обидва варіанти повертають однакову кількість рядків.");
}

static void PrintGoods(IReadOnlyList<Goods> items)
{
    foreach (Goods item in items) Console.WriteLine($"  {item}");
}

static void PrintHelp()
{
    Console.WriteLine("Лабораторна 8: Async/Await, Cancellation, Unit-тести для Warehouse");
    Console.WriteLine("Асинхронний імпорт: dotnet run --project src/Cli -- --file [--path <файл.json>] [--stream] [--cancel] [--timeout <ms/s>] [--progress]");
    Console.WriteLine("Звіти (Lab 7): report-top | report-low | report-groups | report-sum | report-batches | report-top-batches | report-benchmark");
}
