using System.Diagnostics;
using Core.Domain;

namespace Core.Reports;

public static class ReportService
{
    // Товари з залишком меншим за поріг, спочатку за залишком, потім за SKU.
    public static IReadOnlyList<StockLine> LowStock(IEnumerable<Goods> source, int threshold)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source
            .Where(item => item.Quantity < threshold)
            .OrderBy(item => item.Quantity)
            .ThenBy(item => item.Sku, StringComparer.Ordinal)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .Select(ToStockLine)
            .ToList()
            .AsReadOnly();
    }

    // Детермінований top-N: однаковий залишок розв'язуємо за SKU, потім за Id.
    public static IReadOnlyList<StockLine> TopByQuantity(IEnumerable<Goods> source, int count)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count), count, "Кількість позицій не може бути від'ємною.");

        // Спершу фіксуємо другий і третій ключі. OrderByDescending у TopBy стабільний.
        return source
            .OrderBy(item => item.Sku, StringComparer.Ordinal)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .TopBy(item => item.Quantity, count)
            .Select(ToStockLine)
            .ToList()
            .AsReadOnly();
    }

    // Групування за одиницею вимірювання без урахування регістру.
    public static IReadOnlyList<GroupSummary> ByUnit(IEnumerable<Goods> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        // Query syntax: group ... by ... into. UpperInvariant дає групування без урахування регістру.
        var query =
            from item in source
            group item by item.Unit.ToUpperInvariant() into unitGroup
            let total = unitGroup.Sum(item => item.Quantity)
            select new GroupSummary(unitGroup.Key.ToLowerInvariant(), unitGroup.Count(), total);

        return query
            .OrderByDescending(summary => summary.TotalQuantity)
            .ThenBy(summary => summary.Key, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.Key, StringComparer.Ordinal)
            .ToList()
            .AsReadOnly();
    }

    // Агрегати рахуються зі знімка, тому Average/Max безпечні й для порожнього джерела.
    public static StockTotals Totals(IEnumerable<Goods> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        List<Goods> items = source.ToList();
        return items.Count == 0
            ? new StockTotals(0, 0, 0d, 0)
            : new StockTotals(
                items.Count,
                items.Sum(item => item.Quantity),
                items.Average(item => item.Quantity),
                items.Max(item => item.Quantity));
    }

    // LINQ to Objects перевіряє умову під час Count(), а ToList() фіксує знімок.
    public static DeferredExecutionResult DemonstrateDeferredExecution(IEnumerable<Goods> source, int threshold)
    {
        ArgumentNullException.ThrowIfNull(source);
        var items = source.ToList();
        IEnumerable<Goods> query = items.Where(item => item.Quantity < threshold);
        int beforeChange = query.Count();

        items.Add(Goods.Create("DEFERRED-007", "SKU-DEFERRED", "Товар для LINQ-демо", "шт", 3));
        int afterChange = query.Count();

        List<Goods> snapshot = query.ToList();
        items.Clear();
        return new DeferredExecutionResult(beforeChange, afterChange, snapshot.Count);
    }

    // Додаткове 1: Join товарів і пов'язаних партій за GoodsId.
    public static IReadOnlyList<BatchLine> BatchesByGoods(IEnumerable<Goods> goods, IEnumerable<Batch> batches)
    {
        ArgumentNullException.ThrowIfNull(goods);
        ArgumentNullException.ThrowIfNull(batches);
        return goods
            .Join(
                batches,
                goods => goods.Id,
                batch => batch.GoodsId,
                (goods, batch) => new BatchLine(
                    batch.Id,
                    goods.Sku,
                    goods.Name,
                    batch.Quantity,
                    batch.ReceivedDate,
                    batch.Status),
                StringComparer.OrdinalIgnoreCase)
            .OrderBy(line => line.ReceivedDate)
            .ThenBy(line => line.BatchId, StringComparer.Ordinal)
            .ToList()
            .AsReadOnly();
    }

    // Додаткове 2: TopBy<T> використано вдруге для вже сформованого Join-звіту.
    public static IReadOnlyList<BatchLine> TopBatchesByQuantity(
        IEnumerable<Goods> goods, IEnumerable<Batch> batches, int count)
    {
        return BatchesByGoods(goods, batches)
            .OrderBy(line => line.BatchId, StringComparer.Ordinal)
            .TopBy(line => line.BatchQuantity, count);
    }

    // Додаткове 4: два рівнозначні конвеєри на 100 000 згенерованих записів.
    public static LinqBenchmark BenchmarkTopByQuantity(int recordCount = 100_000, int count = 5)
    {
        if (recordCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(recordCount), recordCount, "Кількість записів має бути більшою за нуль.");
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count), count, "Кількість позицій не може бути від'ємною.");

        List<Goods> source = Enumerable.Range(1, recordCount)
            .Select(index => Goods.Create(
                $"BENCH-{index:D6}",
                $"SKU-{index:D6}",
                "Згенерований товар",
                "шт",
                (index * 73) % 1_000))
            .ToList();

        var withoutMiddleToList = Stopwatch.StartNew();
        List<StockLine> direct = source
            .Where(item => item.Quantity >= 0)
            .OrderByDescending(item => item.Quantity)
            .ThenBy(item => item.Sku, StringComparer.Ordinal)
            .Take(count)
            .Select(ToStockLine)
            .ToList();
        withoutMiddleToList.Stop();

        var withMiddleToList = Stopwatch.StartNew();
        List<Goods> filtered = source.Where(item => item.Quantity >= 0).ToList();
        List<StockLine> materialized = filtered
            .OrderByDescending(item => item.Quantity)
            .ThenBy(item => item.Sku, StringComparer.Ordinal)
            .Take(count)
            .Select(ToStockLine)
            .ToList();
        withMiddleToList.Stop();

        return new LinqBenchmark(
            recordCount,
            direct.Count,
            materialized.Count,
            withoutMiddleToList.ElapsedMilliseconds,
            withMiddleToList.ElapsedMilliseconds);
    }

    private static StockLine ToStockLine(Goods item) => new(item.Sku, item.Name, item.Quantity, item.Unit);
}
