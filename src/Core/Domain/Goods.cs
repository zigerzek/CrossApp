using Core.Dto;

namespace Core.Domain;

public sealed class Goods
{
    private int _quantity;

    public string Id { get; }
    public string Sku { get; }
    public string Name { get; }
    public string Unit { get; }
    public string? Note { get; }
    public int Quantity => _quantity;

    private Goods(string id, string sku, string name, string unit, int quantity, string? note)
    {
        Id = id;
        Sku = sku;
        Name = name;
        Unit = unit;
        _quantity = quantity;
        Note = note;
    }

    public static Goods Create(string id, string sku, string name, string unit,
        int quantity, string? note = null)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Ідентифікатор товару обов'язковий", nameof(id));
        if (string.IsNullOrWhiteSpace(sku))
            throw new ArgumentException("SKU не може бути порожнім", nameof(sku));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Назва товару не може бути порожньою", nameof(name));
        if (string.IsNullOrWhiteSpace(unit))
            throw new ArgumentException("Одиниця вимірювання не може бути порожньою", nameof(unit));
        if (quantity < 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity,
                "Початковий залишок не може бути від'ємним");

        return new Goods(id.Trim(), sku.Trim().ToUpperInvariant(), name.Trim(), unit.Trim(),
            quantity, note);
    }

    public void RegisterArrival(int amount)
    {
        ValidateArrival(amount);
        _quantity += amount;
    }

    public void Issue(int amount)
    {
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), amount,
                "Кількість видачі має бути більшою за нуль");
        if (amount > _quantity)
            throw new InvalidOperationException(
                $"Не можна видати {amount} {Unit}: залишок товару {Sku} становить {_quantity} {Unit}");

        _quantity -= amount;
    }

    // Правило двох сутностей: партія належить цьому товару й оприбутковується лише один раз.
    public void RegisterArrival(Batch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);
        if (!string.Equals(batch.GoodsId, Id, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Партія {batch.Id} належить товару {batch.GoodsId}, а не {Id}");

        batch.EnsureCanReceive();
        ValidateArrival(batch.Quantity);

        // Усі перевірки обох сутностей завершено до першої зміни стану.
        batch.MarkReceived();
        _quantity += batch.Quantity;
    }

    private void ValidateArrival(int amount)
    {
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), amount,
                "Кількість приходу має бути більшою за нуль");
        if (amount > int.MaxValue - _quantity)
            throw new InvalidOperationException(
                $"Не можна додати {amount}: залишок {Sku} = {_quantity}, максимум = {int.MaxValue}");
    }

    public GoodsDto ToDto() => new(Id, Sku, Name, Unit, Quantity, Note);

    public static Goods FromDto(GoodsDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return Create(dto.Id, dto.Sku, dto.Name, dto.Unit, dto.Quantity, dto.Note);
    }

    public override string ToString() => $"{Id} [{Sku}] {Name} — {Quantity} {Unit}";
}
