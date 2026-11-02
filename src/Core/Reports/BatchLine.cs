using Core.Domain;

namespace Core.Reports;

// DTO рядка Join-звіту: пов'язує партію з товаром, якому вона належить.
public sealed record BatchLine(
    string BatchId,
    string GoodsSku,
    string GoodsName,
    int BatchQuantity,
    DateOnly ReceivedDate,
    BatchStatus Status);
