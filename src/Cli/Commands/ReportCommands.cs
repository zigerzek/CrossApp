using Cli.Composition;
using Core;
using Core.Abstractions;
using Core.Domain;
using Core.Reports;
using Core.Services;
using Core.Storage;

namespace Cli.Commands;

/// <summary>Звіти лаб. 7 (LINQ) над сховищем, вибраним прапорцями лаб. 5 (--file, --cache, --factory, --list-only).</summary>
internal static class ReportCommands
{
    public static int Run(CommandContext ctx)
    {
        (string command, string[] storeArguments) = ArgumentSplitter.Split(ctx.Args.Rest);
        StoreOptions options = StoreOptions.Parse(storeArguments);
        ICatalogStore store = options.UseFactory
            ? StoreFactory.Create(storeArguments)
            : CreateStore(options);
        var service = new CatalogService(store);
        IReadOnlyList<Goods> items = service.All();

        Console.WriteLine("CrossApp — Warehouse (лаб. 9: конфігурація, шляхи, JSON, secrets)");
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
                HelpText.Print();
                return 1;
        }

        DeferredExecutionResult deferred = ReportService.DemonstrateDeferredExecution(items, 10);
        Console.WriteLine("\n=== Відкладене виконання LINQ ===");
        Console.WriteLine($"До зміни джерела: {deferred.BeforeChange}");
        Console.WriteLine($"Після додавання товару: {deferred.AfterChange}");
        Console.WriteLine($"Знімок після очищення джерела: {deferred.SnapshotAfterClear}");
        return 0;
    }

    private static ICatalogStore CreateStore(StoreOptions options)
    {
        ICatalogStore store = options.UseFile
            ? new FileCatalogStore(options.DataPath)
            : new InMemoryCatalogStore(SampleData.Goods());
        return options.UseCache ? new CachingCatalogStore(store) : store;
    }

    private static void PrintStockLines(IReadOnlyList<StockLine> lines)
    {
        if (lines.Count == 0)
        {
            Console.WriteLine("  Немає рядків для цього звіту.");
            return;
        }

        foreach (StockLine line in lines)
            Console.WriteLine($"  {line.Sku,-10} {line.Name,-24} {line.Quantity,6} {line.Unit}");
    }

    private static void PrintGroups(IReadOnlyList<GroupSummary> groups)
    {
        if (groups.Count == 0)
        {
            Console.WriteLine("  Немає рядків для цього звіту.");
            return;
        }

        foreach (GroupSummary group in groups)
            Console.WriteLine($"  {group.Key,-6} позицій: {group.Count,3}; сумарно: {group.TotalQuantity,6}");
    }

    private static void PrintTotals(StockTotals totals) =>
        Console.WriteLine($"  позицій {totals.Items}, всього {totals.TotalQuantity}, середнє {totals.AverageQuantity:F2}, макс {totals.MaxQuantity}");

    private static void PrintBatchLines(IReadOnlyList<BatchLine> lines)
    {
        if (lines.Count == 0)
        {
            Console.WriteLine("  Немає партій, пов'язаних із товарами поточного сховища.");
            return;
        }

        foreach (BatchLine line in lines)
            Console.WriteLine($"  {line.BatchId,-6} {line.GoodsSku,-10} {line.GoodsName,-24} {line.BatchQuantity,5} {line.ReceivedDate:yyyy-MM-dd} {line.Status}");
    }

    private static void PrintBenchmark(LinqBenchmark benchmark)
    {
        Console.WriteLine($"  Записів: {benchmark.RecordCount}; top-N: {benchmark.ResultCountWithoutMiddleToList} / {benchmark.ResultCountWithMiddleToList}");
        Console.WriteLine($"  Без ToList у середині: {benchmark.WithoutMiddleToListMilliseconds} мс");
        Console.WriteLine($"  З ToList у середині:   {benchmark.WithMiddleToListMilliseconds} мс");
        Console.WriteLine("  Числа залежать від комп'ютера; обидва варіанти повертають однакову кількість рядків.");
    }

    private static void PrintGoods(IReadOnlyList<Goods> items)
    {
        foreach (Goods item in items) Console.WriteLine($"  {item}");
    }
}
