using System.Reflection;
using Core.Domain;
using Core.Dto;
using Core.Import;

int passed = 0;
int failed = 0;
DateOnly date = new(2026, 10, 12);

void Check(string title, Action test)
{
    try
    {
        test();
        passed++;
        Console.WriteLine($"PASS {title}");
    }
    catch (Exception ex)
    {
        failed++;
        Console.WriteLine($"FAIL {title}: {ex.Message}");
    }
}

Check("Goods normalizes required strings and preserves Note", () =>
{
    Goods goods = Goods.Create(" G-1 ", " sku-1 ", " Cement ", " kg ", 0, " note ");
    Equal(new GoodsDto("G-1", "SKU-1", "Cement", "kg", 0, " note "), goods.ToDto());
    Equal(goods.ToDto(), Goods.FromDto(goods.ToDto()).ToDto());
    
});

foreach (string? value in new string?[] { null, "", "   " })
{
    foreach (string field in new[] { "id", "sku", "name", "unit" })
        Check($"Goods rejects {field}='{value ?? "null"}' through factory and DTO", () =>
        {
            GoodsDto dto = new(field == "id" ? value! : "G-1", field == "sku" ? value! : "SKU-1",
                field == "name" ? value! : "Cement", field == "unit" ? value! : "kg", 0);
            Throws<ArgumentException>(() => Goods.Create(dto.Id, dto.Sku, dto.Name, dto.Unit, 0));
            Throws<ArgumentException>(() => Goods.FromDto(dto));
        });
}

foreach (int quantity in new[] { -1, int.MinValue })
    Check($"Goods rejects initial quantity {quantity}", () =>
    {
        Throws<ArgumentOutOfRangeException>(() => MakeGoods(quantity));
        Throws<ArgumentOutOfRangeException>(() => Goods.FromDto(new("G-1", "SKU-1", "Cement", "kg", quantity)));
    });

Check("Arrival and issue including exact depletion", () =>
{
    Goods goods = MakeGoods(100);
    goods.RegisterArrival(50);
    goods.Issue(30);
    Equal(120, goods.Quantity);
    goods.Issue(120);
    Equal(0, goods.Quantity);
});


foreach (int amount in new[] { 0, -1, int.MinValue })
    Check($"Invalid operation amount {amount} leaves state intact", () =>
    {
        Goods goods = MakeGoods(120);
        GoodsDto before = goods.ToDto();
        Throws<ArgumentOutOfRangeException>(() => goods.RegisterArrival(amount));
        Throws<ArgumentOutOfRangeException>(() => goods.Issue(amount));
        Equal(before, goods.ToDto());
    });

Check("Over-issue fails without mutation", () =>
{
    Goods goods = MakeGoods(120);
    Throws<InvalidOperationException>(() => goods.Issue(1000));
    Equal(120, goods.Quantity);
});
Check("Maximum stock and overflow are safe", () =>
{
    Goods goods = MakeGoods(int.MaxValue - 1);
    goods.RegisterArrival(1);
    Throws<InvalidOperationException>(() => goods.RegisterArrival(1));
    Equal(int.MaxValue, goods.Quantity);
});

foreach (string? value in new string?[] { null, "", " " })
    foreach (bool id in new[] { true, false })
        Check($"Batch rejects {(id ? "Id" : "GoodsId")}='{value ?? "null"}'", () =>
        {
            BatchDto dto = new(id ? value! : "B-1", id ? "G-1" : value!, 1, date);
            Throws<ArgumentException>(() => Batch.Create(dto.Id, dto.GoodsId, dto.Quantity, dto.ReceivedDate));
            Throws<ArgumentException>(() => Batch.FromDto(dto));
        });

foreach (int quantity in new[] { 0, -1, int.MinValue })
    Check($"Batch rejects quantity {quantity}", () =>
    {
        Throws<ArgumentOutOfRangeException>(() => Batch.Create("B-1", "G-1", quantity, date));
        Throws<ArgumentOutOfRangeException>(() => Batch.FromDto(new("B-1", "G-1", quantity, date)));
    });
Check("Batch rejects unspecified date", () =>
{
    Throws<ArgumentOutOfRangeException>(() => Batch.Create("B-1", "G-1", 1, default));
    Throws<ArgumentOutOfRangeException>(() => Batch.FromDto(new("B-1", "G-1", 1, default)));
});
Check("Batch normalizes keys and preserves explicit date", () =>
{
    Batch batch = Batch.Create(" B-1 ", " G-1 ", 1, date);
    Equal(new BatchDto("B-1", "G-1", 1, date), batch.ToDto());
});

foreach (BatchStatus status in Enum.GetValues<BatchStatus>())
{
    Check($"Batch DTO round trip for {status}", () =>
    {
        BatchDto dto = new("B-1", "G-1", 20, date, status.ToString());
        Equal(dto, Batch.FromDto(dto).ToDto());
    });
    foreach (bool receive in new[] { true, false })
        Check($"Transition {status} -> {(receive ? "Received" : "Cancelled")}", () =>
        {
            Goods goods = MakeGoods(120);
            Batch batch = Batch.FromDto(new("B-1", "G-1", 20, date, status.ToString()));
            Action action = receive ? () => goods.RegisterArrival(batch) : batch.Cancel;
            if (status == BatchStatus.Draft)
            {
                action();
                Equal(receive ? BatchStatus.Received : BatchStatus.Cancelled, batch.Status);
                Equal(receive ? 140 : 120, goods.Quantity);
            }
            else
            {
                Throws<InvalidOperationException>(action);
                Equal(status, batch.Status);
                Equal(120, goods.Quantity);
            }
        });
}
foreach (string? status in new string?[] { null, "", "Unknown", "1", "999", "draft", " Draft " })
    Check($"Batch rejects status '{status ?? "null"}'", () =>
        Throws<ArgumentException>(() => Batch.FromDto(new("B-1", "G-1", 1, date, status!))));

Check("Foreign batch leaves both entities unchanged", () =>
{
    Goods goods = MakeGoods(120);
    Batch batch = Batch.Create("B-1", "G-OTHER", 20, date);
    Throws<InvalidOperationException>(() => goods.RegisterArrival(batch));
    Equal(120, goods.Quantity);
    Equal(BatchStatus.Draft, batch.Status);
});
Check("Overflow while receiving batch leaves both entities unchanged", () =>
{
    Goods goods = MakeGoods(int.MaxValue);
    Batch batch = Batch.Create("B-1", goods.Id, 1, date);
    Throws<InvalidOperationException>(() => goods.RegisterArrival(batch));
    Equal(int.MaxValue, goods.Quantity);
    Equal(BatchStatus.Draft, batch.Status);
});
Check("Null domain inputs are rejected", () =>
{
    Throws<ArgumentNullException>(() => Goods.FromDto(null!));
    Throws<ArgumentNullException>(() => Batch.FromDto(null!));
    Throws<ArgumentNullException>(() => MakeGoods().RegisterArrival(null!));
});

foreach (Type type in new[] { typeof(Goods), typeof(Batch) })
    Check($"{type.Name} has no public constructors or setters", () =>
    {
        Equal(0, type.GetConstructors().Length);
        Equal(0, type.GetFields(BindingFlags.Public | BindingFlags.Instance).Length);
        Equal(0, type.GetProperties().Count(property => property.SetMethod?.IsPublic == true));
    });

Check("Import retains errors and physical line numbers through comments and blanks", () =>
{
    string path = Path.GetTempFileName();
    try
    {
        File.WriteAllLines(path, ["id;sku;name;unit;quantity", "G-1;sku-1;Cement;kg;10", "",
            "# comment", ";SKU-2;Sand;t;5", "G-3;SKU-3;Paint;;2", "G-4;SKU-4;Bad;kg;text",
            "G-5;SKU-5;Plaster;kg;0"]);
        ImportResult<GoodsDto> source = GoodsCsvImporter.Load(path);
        ImportResult<Goods> result = GoodsDomainMapper.Map(source);
        Equal(2, result.Items.Count);
        Equal(3, result.Errors.Count);
        Equal("2,8", string.Join(',', result.SourceLineNumbers!));
        foreach (int number in new[] { 5, 6, 7 })
            True(result.Errors.Any(error => error.StartsWith($"рядок {number}:")));
        Equal(1, source.Errors.Count);
        Throws<NotSupportedException>(() => ((IList<Goods>)result.Items).Clear());
        Throws<NotSupportedException>(() => ((IList<string>)result.Errors).Clear());
        Throws<NotSupportedException>(() => ((IList<int>)result.SourceLineNumbers!).Clear());
        Throws<NotSupportedException>(() => ((IList<GoodsDto>)source.Items).Clear());
    }
    finally { File.Delete(path); }
});
Check("Mapper supports DTO sources without physical line numbers", () =>
{
    ImportResult<GoodsDto> source = new([MakeGoods().ToDto(), new("", "SKU", "Name", "kg", 1), null!], ["previous"]);
    ImportResult<Goods> result = GoodsDomainMapper.Map(source);
    Equal(1, result.Items.Count);
    Equal(3, result.Errors.Count);
    Equal("previous", result.Errors[0]);
    True(result.Errors[1].StartsWith("запис 2:"));
    True(result.Errors[2].StartsWith("запис 3:"));
    True(result.SourceLineNumbers is null);
});
Check("Mapper rejects invalid metadata and null inputs", () =>
{
    Throws<ArgumentNullException>(() => GoodsDomainMapper.Map(null!));
    Throws<ArgumentNullException>(() => GoodsDomainMapper.Map(new(null!, [])));
    Throws<ArgumentNullException>(() => GoodsDomainMapper.Map(new([], null!)));
    Throws<ArgumentException>(() => GoodsDomainMapper.Map(new([MakeGoods().ToDto()], [], [])));
    Throws<ArgumentException>(() => GoodsDomainMapper.Map(new([MakeGoods().ToDto()], [], [0])));
    Equal(0, GoodsDomainMapper.Map(new([], [], [])).Items.Count);
});

Console.WriteLine($"Checks: {passed} passed, {failed} failed");
return failed == 0 ? 0 : 1;

static Goods MakeGoods(int quantity = 0) => Goods.Create("G-1", "SKU-1", "Cement", "kg", quantity);
static void True(bool condition)
{
    if (!condition) throw new InvalidOperationException("Condition is false");
}
static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"Expected {expected}, got {actual}");
}
static void Throws<TException>(Action action) where TException : Exception
{
    try { action(); }
    catch (Exception ex)
    {
        if (ex.GetType() != typeof(TException))
            throw new InvalidOperationException($"Expected {typeof(TException).Name}, got {ex.GetType().Name}");
        True(!string.IsNullOrWhiteSpace(ex.Message));
        return;
    }
    throw new InvalidOperationException($"Expected {typeof(TException).Name}, no exception thrown");
}
