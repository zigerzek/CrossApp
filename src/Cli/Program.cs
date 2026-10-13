using Core.Domain;
using Core.Dto;
using Core.Import;

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.WriteLine("Лабораторна робота 4 — доменна модель Warehouse");

Console.WriteLine("\n=== Сценарій 1: успішні операції ===");
Goods goods = Goods.Create("G-001", "sku-001", "Цемент М400 25кг", "шт", 100,
    "Зберігати в сухому місці");
Console.WriteLine($"Створено: {goods}");
goods.RegisterArrival(50);
Console.WriteLine($"Прихід 50: {goods}");
goods.Issue(30);
Console.WriteLine($"Видача 30: {goods}");

GoodsDto before = goods.ToDto();
bool passed = true;
Console.WriteLine("\n=== Сценарій 2: порушення інваріантів ===");
passed &= ExpectFailure<InvalidOperationException>("Видача понад залишок", () => goods.Issue(1000));
passed &= ExpectFailure<ArgumentException>("Порожній SKU",
    () => Goods.Create("G-002", " ", "Пісок", "т", 10));
passed &= ExpectFailure<ArgumentOutOfRangeException>("Від'ємний залишок",
    () => Goods.Create("G-003", "SKU-003", "Цегла", "шт", -5));
passed &= ExpectFailure<ArgumentOutOfRangeException>("Нульовий прихід", () => goods.RegisterArrival(0));
passed &= ExpectFailure<ArgumentOutOfRangeException>("Від'ємна видача", () => goods.Issue(-1));
passed &= ExpectFailure<ArgumentException>("Порожня одиниця вимірювання",
    () => Goods.Create("G-004", "SKU-004", "Фарба", " ", 10));
passed &= goods.ToDto() == before;
Console.WriteLine($"Після відмов: {goods}; стан незмінний: {goods.ToDto() == before}");

Console.WriteLine("\n=== Мапінг сутність ↔ DTO ===");
Goods restored = Goods.FromDto(goods.ToDto());
Console.WriteLine($"Відновлено: {restored}; усі дані збережено: {restored.ToDto() == goods.ToDto()}");
passed &= restored.ToDto() == goods.ToDto();
passed &= ExpectFailure<ArgumentOutOfRangeException>("Некоректний DTO",
    () => Goods.FromDto(goods.ToDto() with { Quantity = -1 }));

Console.WriteLine("\n=== Додаткове завдання: дві сутності та стани ===");
Batch batch = Batch.Create("B-001", goods.Id, 20, new DateOnly(2026, 10, 12));
Console.WriteLine($"До оприбуткування: {batch}; залишок {goods.Quantity}");
goods.RegisterArrival(batch);
Console.WriteLine($"Після оприбуткування: {batch}; залишок {goods.Quantity}");
GoodsDto beforeBatchFailures = goods.ToDto();
BatchDto receivedSnapshot = batch.ToDto();
passed &= ExpectFailure<InvalidOperationException>("Повторне оприбуткування",
    () => goods.RegisterArrival(batch));
passed &= ExpectFailure<InvalidOperationException>("Скасування отриманої партії", batch.Cancel);

Batch foreign = Batch.Create("B-002", "G-OTHER", 10, new DateOnly(2026, 10, 12));
passed &= ExpectFailure<InvalidOperationException>("Партія іншого товару",
    () => goods.RegisterArrival(foreign));
Batch cancelled = Batch.Create("B-003", goods.Id, 5, new DateOnly(2026, 10, 12));
cancelled.Cancel();
Console.WriteLine($"Дозволений перехід Draft → Cancelled: {cancelled.Status}");
passed &= ExpectFailure<InvalidOperationException>("Оприбуткування скасованої партії",
    () => goods.RegisterArrival(cancelled));
passed &= ExpectFailure<InvalidOperationException>("Повторне скасування", cancelled.Cancel);
bool unchanged = goods.ToDto() == beforeBatchFailures && batch.ToDto() == receivedSnapshot &&
    foreign.Status == BatchStatus.Draft && cancelled.Status == BatchStatus.Cancelled;
Console.WriteLine($"Після відмов залишок {goods.Quantity}; стани обох сутностей збережено: {unchanged}");
passed &= unchanged;
Batch restoredBatch = Batch.FromDto(batch.ToDto());
Console.WriteLine($"Відновлення партії з DTO: {restoredBatch}; дані збережено: {restoredBatch.ToDto() == batch.ToDto()}");
passed &= restoredBatch.ToDto() == batch.ToDto();

Console.WriteLine("\n=== Додаткове завдання: імпорт → доменні сутності ===");
string path = args.Length > 0 ? args[0] : Path.Combine("data", "lab04.csv");
try
{
    ImportResult<Goods> result = GoodsDomainMapper.Map(GoodsCsvImporter.Load(path));
    Console.WriteLine($"Доменних сутностей: {result.Items.Count}; помилок: {result.Errors.Count}");
    foreach (Goods item in result.Items)
        Console.WriteLine($"  {item}");
    foreach (string error in result.Errors)
        Console.WriteLine($"  ! {error}");
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
{
    Console.WriteLine($"Не вдалося імпортувати '{path}': {ex.Message}");
    return 1;
}

Console.WriteLine(passed ? "\nДемонстрацію лабораторної 4 завершено успішно." : "\nПомилка перевірки інваріантів.");
return passed ? 0 : 1;

static bool ExpectFailure<TException>(string title, Action action) where TException : Exception
{
    try
    {
        action();
        Console.WriteLine($"  ПОМИЛКА: {title} — очікуваний виняток не виник");
        return false;
    }
    catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
    {
        Console.WriteLine($"  {title}: {ex.GetType().Name} — {ex.Message}");
        return ex.GetType() == typeof(TException);
    }
}
