namespace Core.Reports;

// Підсумковий DTO для агрегатного звіту.
public sealed record StockTotals(int Items, int TotalQuantity, double AverageQuantity, int MaxQuantity);
