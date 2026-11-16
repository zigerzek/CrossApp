# CrossApp — Лабораторна робота 8

## Тема: async/await, скасування операцій, доменні винятки та unit-тести Core

**Предметна область:** склад будівельних матеріалів (**Warehouse**), сутність `Goods`.  
У проєкті реалізовано асинхронне введення-виведення для тривалих операцій (імпорт каталогу), кооперативне скасування через `CancellationToken`, розмежування доменних (`DomainException`) і технічних винятків, а також повноцінне покриття шару `Core` модульними тестами `xUnit`.

---

## Швидкий запуск у Git Bash

```bash
cd /c/dev/CrossApp
dotnet build CrossApp.slnx

# 1. Запуск асинхронного імпорту за замовчуванням (імпортує 17 товарів)
dotnet run --project src/Cli -f net10.0 --no-build -- --file

# 2. Перевірка скасування (через прапорець скасування або таймаут)
dotnet run --project src/Cli -f net10.0 --no-build -- --file --cancel
dotnet run --project src/Cli -f net10.0 --no-build -- --file --timeout 1ms

# 3. Перевірка реакції на пошкоджений файл (DomainException)
dotnet run --project src/Cli -f net10.0 --no-build -- --file --path data/bad_sample.json

# 4. Перевірка реакції на відсутній файл (FileNotFoundException)
dotnet run --project src/Cli -f net10.0 --no-build -- --file --path missing.json

# 5. Додаткове завдання 1: потоковий імпорт через IAsyncEnumerable
dotnet run --project src/Cli -f net10.0 --no-build -- --file --stream

# 6. Додаткове завдання 3: підписник прогресу IProgress<int>
dotnet run --project src/Cli -f net10.0 --no-build -- --file --progress

# 7. Запуск усіх unit-тестів Core.Tests (xUnit)
dotnet test

# 8. Додаткове завдання 4: збір покриття коду тестами (Code Coverage)
dotnet test --collect:"XPlat Code Coverage"
```

---

## Ключові зміни та реалізація

### 1. Доменний виняток (`DomainException`)
Створено клас `src/Core/DomainException.cs`. Використовується для позначення порушень бізнес-правил предметної області складу:
- У файлі імпорту: якщо значення залишку товару дорівнює нулю або є від'ємним (`«Запис {N}: кількість не може бути 0.»`);
- Якщо файл не містить списку сутностей або дані пошкоджені.

Вхідні параметри методів (наприклад, `null` чи порожній рядок) перевіряються через `ArgumentException` та `ArgumentOutOfRangeException`. Технічні помилки інфраструктури (`FileNotFoundException`, `IOException`, `JsonException`) не маскуються безпідставно під доменні й обробляються окремо.

### 2. Справжнє асинхронне введення-виведення без `Task.Run`
У `JsonCatalogImporter.cs` операція читання реалізована за допомогою `FileStream` із прапорцем `useAsync: true` та нативного асинхронного десеріалізатора `JsonSerializer.DeserializeAsync`:
```csharp
await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
var dtos = await JsonSerializer.DeserializeAsync<List<GoodsDto>>(stream, JsonOptions, cancellationToken: ct);
```
Ми не використовуємо `Task.Run` навколо блокуючого коду, що гарантує збереження потоків пулу для корисної роботи під час очікування операцій I/O.

### 3. Кооперативне скасування (`CancellationToken`)
- Токен `CancellationToken` передається в усі асинхронні виклики (`stream`, `DeserializeAsync`).
- У циклах імпорту стан токена перевіряється на кожній ітерації через `ct.ThrowIfCancellationRequested()`.
- У `Cli/Program.cs` зареєстровано обробник комбінації клавіш `Ctrl+C` через `Console.CancelKeyPress`.
- `OperationCanceledException` обробляється окремим першим блоком `catch` і виводить повідомлення `«Імпорт скасовано.»` без стектрейсу.

### 4. Додаткові завдання (на вищий бал)
1. **`IAsyncEnumerable<Goods>` та `[EnumeratorCancellation]`:**  
   У методі `JsonCatalogImporter.ReadStreamAsync` реалізовано потокове зчитування елементів через `JsonSerializer.DeserializeAsyncEnumerable` та оператор `yield return`. У `CatalogService.ImportStreamAsync` читання виконується через `await foreach`.
2. **Зв'язані токени (`CancellationTokenSource.CreateLinkedTokenSource`):**  
   У тесті `Import_WithLinkedTokenSource_ProvesTimeoutTriggered` об'єднано користувацький токен та токен таймауту, доводячи, що скасування відбулося саме через завершення таймауту.
3. **`IProgress<int>`:**  
   `CatalogService.ImportAsync` та `ImportStreamAsync` приймають опціональний `IProgress<int>` і повідомляють про відсоток виконання, а CLI малює прогрес у консолі.
4. **Покриття коду тестами (`XPlat Code Coverage`):**  
   Проєкт `Core.Tests` використовує `coverlet.collector`. Команда `dotnet test --collect:"XPlat Code Coverage"` генерує детальний звіт покриття `coverage.cobertura.xml`.

---

## Тестування (`xUnit`)

Проєкт `tests/Core.Tests` посилається виключно на `Core` і не має залежностей від `Cli`:
- **`CatalogServiceTests`**: інваріанти сервісу, збереження стану при помилках, виклик події `Changed` рівно один раз.
- **`ReportServiceTests`**: теорії `[Theory]` з `[InlineData]` для звіту дефіциту, детермінованість топ-N, агрегати та обробка порожнього джерела.
- **`ImportTests`**: скасування імпорту, доменні винятки при некоректних даних, робота з тимчасовими файлами (очищення у `finally`), ідемпотентність імпорту, потоковий імпорт через `IAsyncEnumerable`, зв'язані токени та `IProgress<int>`.

Усі 16 тестів xUnit, а також усі попередні тестові набори (`DomainChecks` 56, `ServiceChecks` 45, `EventChecks` 34, `ReportChecks` 9) проходять на 100% (разом 160 успішних тестів).

---

## Definition of Done (DoD)

| Пункт DoD | Стан | Пояснення |
| :--- | :---: | :--- |
| 1. Проєкт `tests/Core.Tests` у solution, посилання лише на Core | ✅ | Додано в `CrossApp.slnx`, посилається на `src/Core/Core.csproj` |
| 2. Асинхронне читання через `File*Async` / `FileStream(useAsync: true)` без `Task.Run` | ✅ | `FileStream(..., useAsync: true)` та `JsonSerializer.DeserializeAsync` |
| 3. `CancellationToken` у сигнатурі, перевірка в циклі і передача в await | ✅ | Передається в `LoadAsync`/`ImportAsync`, `ThrowIfCancellationRequested()` у циклах |
| 4. CLI реагує на `Ctrl+C` або таймаут повідомленням без стектрейсу | ✅ | Виводить `Імпорт скасовано.` через окремий `catch (OperationCanceledException)` |
| 5. Власний `DomainException` для бізнес-помилок | ✅ | `DomainException` для порушень правил каталогу та нульових залишків |
| 6. $\ge 5$ тестів: інваріанти, подія, звіт, скасування; `dotnet test` зелений | ✅ | 16 тестів xUnit, 100% пройдено |
| 7. Немає `async void`, `.Result`, `.Wait()` | ✅ | Перевірено по всьому solution (0 входжень) |
| 8. README: запуск тестів і скасування; коміт `lab08` | ✅ | Описано в поточному файлі |
