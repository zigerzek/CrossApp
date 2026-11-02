namespace Core.Reports;

// Результат вимірювання двох еквівалентних LINQ-конвеєрів.
public sealed record LinqBenchmark(
    int RecordCount,
    int ResultCountWithoutMiddleToList,
    int ResultCountWithMiddleToList,
    long WithoutMiddleToListMilliseconds,
    long WithMiddleToListMilliseconds);
