using Core;
using Core.Domain;
using Core.Reports;

int passed = 0, failed = 0;

Check("LowStock filters, orders deterministically and maps to DTO", () =>
{
    var items = new[]
    {
        Goods.Create("3", "SKU-C", "C", "шт", 5),
        Goods.Create("1", "SKU-A", "A", "шт", 5),
        Goods.Create("2", "SKU-B", "B", "шт", 1),
        Goods.Create("4", "SKU-D", "D", "шт", 10)
    };
    IReadOnlyList<StockLine> report = ReportService.LowStock(items, 10);
    Equal("SKU-B,SKU-A,SKU-C", string.Join(',', report.Select(line => line.Sku)));
    Equal(1, report[0].Quantity);
    Throws<NotSupportedException>(() => ((IList<StockLine>)report).Clear());
    Equal(4, items.Length);
});

Check("TopByQuantity uses secondary and tertiary keys and accepts zero", () =>
{
    var items = new[]
    {
        Goods.Create("3", "SKU-A", "A3", "шт", 20),
        Goods.Create("1", "SKU-A", "A1", "шт", 20),
        Goods.Create("2", "SKU-B", "B", "шт", 20),
        Goods.Create("4", "SKU-C", "C", "шт", 19)
    };
    Equal("A1,A3,B", string.Join(',', ReportService.TopByQuantity(items, 3).Select(line => line.Name)));
    Equal(0, ReportService.TopByQuantity(items, 0).Count);
    Throws<ArgumentOutOfRangeException>(() => ReportService.TopByQuantity(items, -1));
});

Check("ByUnit groups case-insensitively, aggregates and orders summaries", () =>
{
    var items = new[]
    {
        Goods.Create("1", "A", "A", "м", 5),
        Goods.Create("2", "B", "B", "М", 7),
        Goods.Create("3", "C", "C", "шт", 12),
        Goods.Create("4", "D", "D", "л", 12)
    };
    IReadOnlyList<GroupSummary> report = ReportService.ByUnit(items);
    Equal(3, report.Count);
    Equal(("л", 1, 12), (report[0].Key, report[0].Count, report[0].TotalQuantity));
    Equal(("м", 2, 12), (report[1].Key, report[1].Count, report[1].TotalQuantity));
    Equal(("шт", 1, 12), (report[2].Key, report[2].Count, report[2].TotalQuantity));
});

Check("Totals returns correct values and handles an empty source", () =>
{
    var items = new[]
    {
        Goods.Create("1", "A", "A", "шт", 2),
        Goods.Create("2", "B", "B", "шт", 5),
        Goods.Create("3", "C", "C", "шт", 8)
    };
    Equal(new StockTotals(3, 15, 5d, 8), ReportService.Totals(items));
    Equal(new StockTotals(0, 0, 0d, 0), ReportService.Totals(Array.Empty<Goods>()));
    Equal(0, ReportService.LowStock(Array.Empty<Goods>(), 10).Count);
    Equal(0, ReportService.ByUnit(Array.Empty<Goods>()).Count);
});

Check("Deferred execution sees a changed source; ToList snapshot survives Clear", () =>
{
    var items = new[]
    {
        Goods.Create("1", "A", "A", "шт", 0),
        Goods.Create("2", "B", "B", "шт", 20)
    };
    Equal(new DeferredExecutionResult(1, 2, 2), ReportService.DemonstrateDeferredExecution(items, 10));
    Equal(2, items.Length);
});

Check("Join report maps related Goods and Batch entities to DTO and ignores unmatched batches", () =>
{
    var goods = new[]
    {
        Goods.Create("G-1", "SKU-1", "Кабель", "м", 20),
        Goods.Create("G-2", "SKU-2", "Фарба", "л", 10)
    };
    var batches = new[]
    {
        Batch.Create("B-2", "g-1", 4, new DateOnly(2026, 10, 2)),
        Batch.Create("B-1", "G-1", 7, new DateOnly(2026, 10, 1)),
        Batch.Create("B-x", "missing", 9, new DateOnly(2026, 10, 3))
    };
    IReadOnlyList<BatchLine> report = ReportService.BatchesByGoods(goods, batches);
    Equal(2, report.Count);
    Equal(new BatchLine("B-1", "SKU-1", "Кабель", 7, new DateOnly(2026, 10, 1), BatchStatus.Draft), report[0]);
    Equal("B-1,B-2", string.Join(',', report.Select(line => line.BatchId)));
    Throws<NotSupportedException>(() => ((IList<BatchLine>)report).Clear());
});

Check("TopBy extension is stable after a deterministic pre-sort and is used for joined batches", () =>
{
    var values = new[] { (Name: "B", Score: 5), (Name: "A", Score: 5), (Name: "C", Score: 4) };
    Equal("B,A", string.Join(',', values.TopBy(value => value.Score, 2).Select(value => value.Name)));
    Throws<ArgumentNullException>(() => EnumerableReportExtensions.TopBy<(string, int)>(null!, value => value.Item2, 1));
    Throws<ArgumentOutOfRangeException>(() => values.TopBy(value => value.Score, -1));

    var goods = new[] { Goods.Create("G-1", "SKU-1", "Товар", "шт", 1) };
    var batches = new[]
    {
        Batch.Create("B-2", "G-1", 10, new DateOnly(2026, 10, 2)),
        Batch.Create("B-1", "G-1", 10, new DateOnly(2026, 10, 1)),
        Batch.Create("B-3", "G-1", 9, new DateOnly(2026, 10, 3))
    };
    Equal("B-1,B-2", string.Join(',', ReportService.TopBatchesByQuantity(goods, batches, 2).Select(line => line.BatchId)));
});

Check("Benchmark generates 100000 records and both equivalent pipelines return top-N", () =>
{
    LinqBenchmark benchmark = ReportService.BenchmarkTopByQuantity();
    Equal(100_000, benchmark.RecordCount);
    Equal(5, benchmark.ResultCountWithoutMiddleToList);
    Equal(5, benchmark.ResultCountWithMiddleToList);
    True(benchmark.WithoutMiddleToListMilliseconds >= 0);
    True(benchmark.WithMiddleToListMilliseconds >= 0);
    Throws<ArgumentOutOfRangeException>(() => ReportService.BenchmarkTopByQuantity(0));
    Throws<ArgumentOutOfRangeException>(() => ReportService.BenchmarkTopByQuantity(1, -1));
});

Check("Report methods reject null sources and seed meets the laboratory data requirements", () =>
{
    Throws<ArgumentNullException>(() => ReportService.LowStock(null!, 10));
    Throws<ArgumentNullException>(() => ReportService.TopByQuantity(null!, 5));
    Throws<ArgumentNullException>(() => ReportService.ByUnit(null!));
    Throws<ArgumentNullException>(() => ReportService.Totals(null!));
    Throws<ArgumentNullException>(() => ReportService.DemonstrateDeferredExecution(null!, 10));
    Throws<ArgumentNullException>(() => ReportService.BatchesByGoods(null!, Array.Empty<Batch>()));
    Throws<ArgumentNullException>(() => ReportService.BatchesByGoods(Array.Empty<Goods>(), null!));
    IReadOnlyList<Goods> seed = SampleData.Goods();
    Equal(18, seed.Count);
    Equal(4, seed.Select(item => item.Unit).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    Equal(4, SampleData.Batches().Count);
});

Console.WriteLine($"Report checks: {passed} passed, {failed} failed");
return failed == 0 ? 0 : 1;

void Check(string name, Action action)
{
    try
    {
        action();
        passed++;
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception ex)
    {
        failed++;
        Console.WriteLine($"FAIL {name}: {ex.GetType().Name}: {ex.Message}");
    }
}

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"Expected {expected}, got {actual}");
}

static void True(bool condition)
{
    if (!condition) throw new InvalidOperationException("Condition is false");
}

static void Throws<TException>(Action action) where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException($"Expected {typeof(TException).Name}");
}
