using Core.Dto;

namespace Core.Domain;

public sealed class Batch
{
    private BatchStatus _status;

    public string Id { get; }
    public string GoodsId { get; }
    public int Quantity { get; }
    public DateOnly ReceivedDate { get; }
    public BatchStatus Status => _status;

    private Batch(string id, string goodsId, int quantity, DateOnly receivedDate)
    {
        Id = id;
        GoodsId = goodsId;
        Quantity = quantity;
        ReceivedDate = receivedDate;
        _status = BatchStatus.Draft;
    }

    public static Batch Create(string id, string goodsId, int quantity, DateOnly receivedDate)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Ідентифікатор партії обов'язковий", nameof(id));
        if (string.IsNullOrWhiteSpace(goodsId))
            throw new ArgumentException("Ідентифікатор товару партії обов'язковий", nameof(goodsId));
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity,
                "Кількість у партії має бути більшою за нуль");
        if (receivedDate == default)
            throw new ArgumentOutOfRangeException(nameof(receivedDate), receivedDate,
                "Дата надходження партії має бути задана");

        return new Batch(id.Trim(), goodsId.Trim(), quantity, receivedDate);
    }

    public void Cancel() => _status = NextStatus(BatchStatus.Cancelled);

    internal void EnsureCanReceive() => _ = NextStatus(BatchStatus.Received);

    internal void MarkReceived() => _status = NextStatus(BatchStatus.Received);

    private BatchStatus NextStatus(BatchStatus target) => (_status, target) switch
    {
        (BatchStatus.Draft, BatchStatus.Received) => BatchStatus.Received,
        (BatchStatus.Draft, BatchStatus.Cancelled) => BatchStatus.Cancelled,
        _ => throw new InvalidOperationException(
            $"Партія {Id}: перехід {_status} → {target} заборонений")
    };

    public BatchDto ToDto() => new(Id, GoodsId, Quantity, ReceivedDate, Status.ToString());

    public static Batch FromDto(BatchDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        Batch batch = Create(dto.Id, dto.GoodsId, dto.Quantity, dto.ReceivedDate);
        if (!Enum.TryParse(dto.Status, out BatchStatus status) ||
            !Enum.IsDefined(status) || status.ToString() != dto.Status)
            throw new ArgumentException($"Невідомий стан партії '{dto.Status}'", nameof(dto));

        // Відновлюємо збережений стан; це не повторне оприбуткування товару.
        batch._status = status;
        return batch;
    }

    public override string ToString() =>
        $"{Id}: товар {GoodsId}, {Quantity} од., {ReceivedDate:yyyy-MM-dd}, стан {Status}";
}
