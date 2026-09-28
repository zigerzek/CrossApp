namespace Core.Dto;

public sealed record GoodsDto(
    string Id,
    string Sku,
    string Name,
    string Unit,
    int Quantity,
    string? Note = null);