namespace Core.Reports;

// Незмінний рядок звіту: звіт не повертає доменну сутність Goods.
public sealed record StockLine(string Sku, string Name, int Quantity, string Unit);
