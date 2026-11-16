# Звіт до лабораторної роботи № 8

**Тема:** `async/await`, скасування операцій, доменні винятки, unit-тести Core  
**Предметна область:** склад будівельних матеріалів (**Warehouse**), сутність `Goods`  
**Мета:** реалізувати асинхронне виконання тривалих операцій введення-виведення, забезпечити підтримку кооперативного скасування, розмежувати доменні й технічні помилки та покрити модульними тестами ключову логіку проєкту Core.

---

## 1. Які операції стали асинхронними і чому саме вони

Асинхронними стали операції імпорту каталогу з диска:
- `ICatalogImporter.LoadAsync(string path, CancellationToken ct)`
- `JsonCatalogImporter.LoadAsync(...)`
- `JsonCatalogImporter.ReadStreamAsync(...)` (потоковий імпорт)
- `CatalogService.ImportAsync(...)` та `CatalogService.ImportStreamAsync(...)`

**Чому саме вони:**  
Читання файлу з диска є операцією введення-виведення (I/O-bound). Тривалість зчитування залежить від швидкості дискової підсистеми, розміру файлу та черги операцій ОС. Завдяки справжньому асинхронному введенню-виведенню через `FileStream(..., useAsync: true)` та `JsonSerializer.DeserializeAsync`, потік пулу потоків не блокується в очікуванні відповіді дискового контролера. Метод призупиняє виконання (`await`), а потік повертається в пул для обслуговування інших завдань (наприклад, HTTP-запитів в API або оновлення UI).

**Які операції залишилися синхронними:**  
1. Операції зі сховищем у пам'яті `InMemoryCatalogStore` (`List`, `GetById`, `Add`, `Update`, `Remove`) та формування звітів `ReportService`. Вони є CPU-bound або миттєвими операціями в пам'яті. Перетворення їх на штучний `Task.Run` було б антипатерном, що призводить до зайвих накладних витрат на перемикання контексту та виділення об'єктів `Task`.
2. Доменні методи сутностей `Goods` та `Batch` (`RegisterArrival`, `Issue`). Бізнес-інваріанти розраховуються синхронно безпосередньо в процесорі.

---

## 2. Фрагменти коду

### Фрагмент `LoadAsync` (справжнє async I/O, скасування, доменний виняток)
```csharp
await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
    FileShare.Read, 4096, useAsync: true);

var dtos = await JsonSerializer.DeserializeAsync<List<GoodsDto>>(
    stream, JsonOptions, cancellationToken: ct)
    ?? throw new DomainException($"Файл {Path.GetFileName(path)} не містить списку.");

for (var i = 0; i < dtos.Count; i++)
{
    ct.ThrowIfCancellationRequested();
    if (dtos[i].Quantity == 0)
        throw new DomainException($"Запис {i + 1}: кількість не може бути 0.");
    result.Add(Goods.FromDto(dtos[i]));
}
```

### Фрагмент обробки скасування в `Cli/Program.cs`
```csharp
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
try
{
    int added = await service.ImportAsync(importer, dataPath, progress, cts.Token);
    Console.WriteLine($"Імпортовано записів: {added}");
}
catch (OperationCanceledException)
{
    Console.WriteLine("Імпорт скасовано.");
}
catch (DomainException ex)
{
    Console.WriteLine($"Дані відхилено: {ex.Message}");
}
```

---

## 3. Таблиця тестів `Core.Tests`

Усі 16 модульних тестів реалізовано з використанням бібліотеки `xUnit`. Тестовий проєкт посилається виключно на `Core` і не має посилання на `Cli`.

| Клас тесту | Назва тесту | Що перевіряє | Очікуваний результат |
| :--- | :--- | :--- | :--- |
| `CatalogServiceTests` | `Receive_WithNegativeQuantity_Throws_AndKeepsState` | Інваріант: прихід із від'ємним числом відхиляється | Викидається `ArgumentOutOfRangeException`, стан залишку незмінний |
| `CatalogServiceTests` | `Add_RaisesChangedExactlyOnce` | Підняття події: додавання товару сповіщає підписників рівно 1 раз | `Assert.Single(events)`, `Kind == Added`, `Delta == 7` |
| `CatalogServiceTests` | `Issue_OverCurrentStock_Throws_AndKeepsState` | Бізнес-правило: заборона видачі понад наявний залишок | `InvalidOperationException`, залишок лишається 10 |
| `CatalogServiceTests` | `Receive_NonExistentItem_Throws` | Перевірка наявності товару за ідентифікатором | `InvalidOperationException` при невідомому id |
| `ReportServiceTests` | `LowStock_CountsOnlyBelowThreshold(10, 2)` | Звіт дефіциту за порогом 10 (`[Theory]`, `[InlineData]`) | Рівно 2 позиції мають залишок < 10 |
| `ReportServiceTests` | `LowStock_CountsOnlyBelowThreshold(1, 0)` | Звіт дефіциту за порогом 1 (`[Theory]`, `[InlineData]`) | 0 позицій мають залишок < 1 |
| `ReportServiceTests` | `TopByQuantity_IsDeterministic` | Детермінованість сортування топ-2 за залишком | Повертає список із фіксованим порядком `SKU-BIG`, `SKU-MID` |
| `ReportServiceTests` | `Totals_CalculatesAggregatesCorrectly` | Обчислення загальних агрегатів (кількість, сума, середнє, максимум) | `Items=4`, `Total=157`, `Average=39.25`, `Max=100` |
| `ReportServiceTests` | `Totals_HandlesEmptySequenceWithoutException` | Безпека обчислення агрегатів над порожньою послідовністю | Повертає нульовий `StockTotals` без `InvalidOperationException` |
| `ImportTests` | `ImportAsync_WhenAlreadyCancelled_Throws` | Кооперативне скасування: поведінка при передачі скасованого токена | Швидке завершення з винятком `OperationCanceledException` |
| `ImportTests` | `ImportAsync_WithCorruptedFile_ThrowsDomainException_AndCleansUpTempFile` | Доменний виняток при нульовій кількості в імпорті та очищення файлу у `finally` | Викидається `DomainException`, файл видаляється |
| `ImportTests` | `ImportAsync_WithMissingList_ThrowsDomainException` | Перевірка вмісту JSON-файлу (null замість масиву) | Викидається `DomainException («не містить списку»)` |
| `ImportTests` | `ImportAsync_WithValidFile_ImportsSuccessfully_AndIsIdempotent` | Успішний імпорт та ідемпотентність при повторному імпорті | Додано 2 записи при першому запуску, 0 при повторному |
| `ImportTests` | `ImportStreamAsync_StreamsItems_UsingAsyncEnumerable` | Додаткове 1: потокове читання через `IAsyncEnumerable` | Потоково імпортовано 2 елементи |
| `ImportTests` | `Import_WithLinkedTokenSource_ProvesTimeoutTriggered` | Додаткове 2: зв'язані токени через `CreateLinkedTokenSource` | Токен таймауту скасовано, користувацький токен чистий |
| `ImportTests` | `ImportAsync_ReportsProgressCorrectly` | Додаткове 3: передача відсотків прогресу через `IProgress<int>` | Звіти прогресу досягають 50% та 100% |

---

## 4. Вивід `dotnet test` та вивід сценаріїв запуску CLI

### Результат `dotnet test`
```text
$ dotnet test
  Determining projects to restore...
  All projects are up-to-date for restore.
  Core -> C:\dev\CrossApp\src\Core\bin\Debug\net10.0\Core.dll
  Core.Tests -> C:\dev\CrossApp\tests\Core.Tests\bin\Debug\net10.0\Core.Tests.dll
Test run for C:\dev\CrossApp\tests\Core.Tests\bin\Debug\net10.0\Core.Tests.dll (.NETCoreApp,Version=v10.0)
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:    16, Skipped:     0, Total:    16, Duration: 249 ms - Core.Tests.dll (net10.0)
```

### Сценарій 1: Успішний імпорт за замовчуванням
```text
$ dotnet run --project src/Cli -f net10.0 --no-build -- --file
Імпортовано записів: 17
```

### Сценарій 2: Скасування імпорту за таймаутом або сигналом Cancel
```text
$ dotnet run --project src/Cli -f net10.0 --no-build -- --file --timeout 1ms
Імпорт скасовано.
```

### Сценарій 3: Відхилення некоректних даних (DomainException)
```text
$ dotnet run --project src/Cli -f net10.0 --no-build -- --file --path data/bad_sample.json
Дані відхилено: Запис 1: кількість не може бути 0.
```

### Сценарій 4: Спроба імпорту відсутнього файлу (FileNotFoundException)
```text
$ dotnet run --project src/Cli -f net10.0 --no-build -- --file --path non_existent.json
Файл не знайдено: C:\dev\CrossApp\non_existent.json
```

---

## 5. Різниця між `DomainException` і `ArgumentException` у проєкті

У проєкті чітко розмежовано призначення типів винятків:
- **`ArgumentException` та `ArgumentOutOfRangeException`** сигналізують про порушення контракту вхідних параметрів конкретного методу: передача `null`, порожнього рядка замість назви чи артикулу товару (`Goods.Create(id, sku, ...)`), або від'ємного значення приходу (`ValidateArrival(amount <= 0)`). Це технічні дефекти виклику API розробником.
- **`DomainException`** відображає порушення бізнес-правил предметної області складу, які виникають під час обробки зовнішніх даних. Наприклад, у файлі постачання товар має залишок 0 (`«Запис {N}: кількість не може бути 0.»`), або імпортований файл взагалі не містить списку товарних позицій. Крім того, якщо створення сутності відхиляється через некоректні дані в рядку файлу, виняток `ArgumentException` обгортається у `DomainException` із зазначенням порядкового номера пошкодженого запису (`«Запис 1: Назва товару не може бути порожньою»`), зберігаючи первинну причину у властивості `InnerException`.

---

## 6. Висновок

У ході лабораторної роботи № 8 впроваджено повноцінне асинхронне введення-виведення на базі `async/await` та `FileStream(useAsync: true)` без використання блокуючого `Task.Run`. Реалізовано кооперативне скасування з передачею `CancellationToken` та окремою безпечною обробкою `OperationCanceledException`. Запроваджено клас `DomainException` для семантичного відокремлення бізнес-помилок від технічних винятків. Створено тестовий проєкт `Core.Tests` на базі `xUnit`, у якому успішно пройдено 16 модульних тестів (включно з усіма 4 додатковими завданнями: потоковим читанням через `IAsyncEnumerable`, зв'язаними токенами, відстеженням прогресу `IProgress<int>` та аналізом покриття коду).
