namespace Core.Dto;

public sealed record BatchDto(
    string Id,
    string GoodsId,
    int Quantity,
    DateOnly ReceivedDate);