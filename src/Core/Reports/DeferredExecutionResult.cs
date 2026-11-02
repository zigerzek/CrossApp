namespace Core.Reports;

// Дані демонстрації відкладеного виконання LINQ-запиту.
public sealed record DeferredExecutionResult(int BeforeChange, int AfterChange, int SnapshotAfterClear);
