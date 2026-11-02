namespace Core.Reports;

// Підсумок однієї групи товарів за одиницею вимірювання.
public sealed record GroupSummary(string Key, int Count, int TotalQuantity);
