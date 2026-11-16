# Інструкція для захисту лабораторної роботи № 8

Посібник побудовано просто та чітко: за 8 пунктами **Definition of Done** та 4 **додатковими завданнями**.

---

## 🎯 Вступна фраза (перші 30 секунд)

> «У 8-й лабораторній роботі ми реалізували асинхронне введення-виведення для операцій імпорту каталогу складу Warehouse.
> 
> Головні принципи: справжній асинхронний I/O без блокуючого `Task.Run`, наскрізне скасування через `CancellationToken`, розмежування доменних помилок (`DomainException`) і технічних збоїв, а також покриття шару `Core` модульними тестами на `xUnit`.
> 
> Усі 8 пунктів Definition of Done та всі 4 додаткові завдання виконано повністю».

---

## 📋 БЛОК 1. Захист по пунктах Definition of Done (DoD)

### Пункт 1. Проєкт `tests/Core.Tests` у solution, посилання лише на `Core`
- **Що показати в коді:** Відкрий файл `tests/Core.Tests/Core.Tests.csproj`.
- **Що сказати:**
  > «Проєкт тестів `Core.Tests` створено на базі xUnit. Він містить посилання `<ProjectReference Include="..\..\src\Core\Core.csproj" />` і **не посилається** на `Cli`. Тести перевіряють чисту бізнес-логіку ядра без прив'язки до консолі».

---

### Пункт 2. Асинхронне читання через `FileStream(useAsync: true)` без `Task.Run`
- **Що показати в коді:** Відкрий `src/Core/Storage/JsonCatalogImporter.cs` (рядки 20–25).
- **Що сказати:**
  > «Ми використовуємо справжній асинхронний I/O: створюємо `FileStream` з прапорцем `useAsync: true` і викликаємо `JsonSerializer.DeserializeAsync(...)`. Ми не огортаємо синхронний код у `Task.Run`, тому робочі потоки з пулу не блокуються під час очікування відповіді диска».

---

### Пункт 3. `CancellationToken` у сигнатурі, перевірка в циклі і передача в `await`
- **Що показати в коді:** 
  1. `src/Core/Storage/JsonCatalogImporter.cs` — токен передається в `DeserializeAsync(..., cancellationToken: ct)`, а в циклі стоїть `ct.ThrowIfCancellationRequested()`.
  2. `src/Core/Services/CatalogService.cs` — метод `ImportAsync(..., CancellationToken ct)` перевіряє `ct.ThrowIfCancellationRequested()` перед додаванням кожного товару.
- **Що сказати:**
  > «Скасування є кооперативним. Токен проходить по всьому ланцюжку: `Cli` $\rightarrow$ `CatalogService` $\rightarrow$ `JsonCatalogImporter` $\rightarrow$ `JsonSerializer`. У кожному циклі стан токена перевіряється через `ThrowIfCancellationRequested()`, тому операцію можна перервати в будь-який момент».

---

### Пункт 4. CLI реагує на Ctrl+C або таймаут повідомленням без стектрейсу
- **Команда в терміналі:**
  ```bash
  # Скасування через прапорець:
  dotnet run --project src/Cli -f net10.0 --no-build -- --file --cancel

  # Або скасування за таймаутом:
  dotnet run --project src/Cli -f net10.0 --no-build -- --file --timeout 1ms
  ```
- **Результат на екрані:** `Імпорт скасовано.` (код завершення 0).
- **Що показати в коді:** `src/Cli/Program.cs` (блок `catch (OperationCanceledException)`).
- **Що сказати:**
  > «У `Program.cs` зареєстровано `Console.CancelKeyPress`, який викликає `cts.Cancel()`. Виняток `OperationCanceledException` перехоплюється першим окремим блоком `catch`. Скасування — це штатне завершення за бажанням користувача, а не збій, тому виводиться чисте повідомлення без стектрейсу».

---

### Пункт 5. Власний `DomainException` для бізнес-помилок
- **Що показати в коді:** Клас `src/Core/DomainException.cs` та перевірку в `src/Core/Storage/JsonCatalogImporter.cs`:
  ```csharp
  if (dto.Quantity == 0)
      throw new DomainException($"Запис {i + 1}: кількість не може бути 0.");
  ```
- **Команда в терміналі:**
  ```bash
  dotnet run --project src/Cli -f net10.0 --no-build -- --file --path data/bad_sample.json
  ```
- **Результат на екрані:** `Дані відхилено: Запис 1: кількість не може бути 0.`
- **Що сказати:**
  > «`ArgumentException` кидається, якщо розробник передав некоректний аргумент у метод (наприклад, `null` замість назви товару). А `DomainException` сигналізує про порушення бізнес-правил предметної області — наприклад, коли у файлі постачання товар має залишок 0. CLI перехоплює його окремим блоком `catch (DomainException ex)`».

---

### Пункт 6. $\ge 5$ тестів: інваріанти, подія, звіт, скасування (`dotnet test` зелений)
- **Команда в терміналі:**
  ```bash
  dotnet test
  ```
- **Результат на екрані:** `Passed! - Failed: 0, Passed: 16, Skipped: 0`
- **Що сказати:**
  > «У нас написано не 5, а **16 тестів**:
  > 1. `Receive_WithNegativeQuantity_Throws_AndKeepsState` — інваріант і незмінність стану;
  > 2. `Add_RaisesChangedExactlyOnce` — підняття події `Changed` рівно 1 раз;
  > 3. `LowStock_CountsOnlyBelowThreshold` — звіт дефіциту через `[Theory]` та `[InlineData]`;
  > 4. `TopByQuantity_IsDeterministic` — детермінований топ-N;
  > 5. `ImportAsync_WhenAlreadyCancelled_Throws` — скасування;
  > 6. `ImportAsync_WithCorruptedFile_ThrowsDomainException...` — робота з тимчасовим файлом, перевірка `DomainException` та видалення файлу у блоці `finally`».

---

### Пункт 7. Немає `async void`, `.Result`, `.Wait()`
- **Що сказати:**
  > «В усьому рішенні немає жодного блокуючого виклику `.Result` або `.Wait()`, що захищає від взаємних блокувань (deadlocks). Також немає методів `async void` (усі асинхронні методи повертають `Task` або `IAsyncEnumerable`), тому будь-який виняток коректно очікується та перехоплюється».

---

### Пункт 8. `README.md` та успішний імпорт за замовчуванням
- **Команда в терміналі:**
  ```bash
  dotnet run --project src/Cli -f net10.0 --no-build -- --file
  ```
- **Результат на екрані:** `Імпортовано записів: 17`
- **Що показати в коді:** Відкрий `README.md`, де описано запуск тестів, команди скасування та таблицю Definition of Done.

---

## 🚀 БЛОК 2. Захист по 4 додаткових завданнях

### Додаткове 1. Потоковий імпорт через `IAsyncEnumerable<Goods>`
- **Команда в терміналі:**
  ```bash
  dotnet run --project src/Cli -f net10.0 --no-build -- --file --stream
  ```
  *(Вивід: `Імпортовано записів: 17`)*
- **Що показати в коді:** 
  1. `src/Core/Storage/JsonCatalogImporter.cs` — метод `ReadStreamAsync` з атрибутом `[EnumeratorCancellation]` та `yield return item`.
  2. `src/Core/Services/CatalogService.cs` — метод `ImportStreamAsync` з обходом через `await foreach`.
- **Що сказати:**
  > «Замість завантаження всього списку в пам'ять ми читаємо JSON як потік об'єктів за допомогою `JsonSerializer.DeserializeAsyncEnumerable`. Метод повертає `IAsyncEnumerable<Goods>`, а сервіс опрацьовує елементи по одному по мірі їх надходження через `await foreach`».

---

### Додаткове 2. Зв'язані токени (`CreateLinkedTokenSource`)
- **Що показати в коді:** Відкрий тест `Import_WithLinkedTokenSource_ProvesTimeoutTriggered` у `tests/Core.Tests/ImportTests.cs`.
- **Що сказати:**
  > «Ми створюємо зв'язане джерело токенів `CancellationTokenSource.CreateLinkedTokenSource(userCts.Token, timeoutCts.Token)`. У тесті ми довели: коли таймаут спливає, `timeoutCts.IsCancellationRequested == true`, зв'язаний токен теж стає скасованим, а користувацький токен `userCts` залишається `false`. Тобто операція перервалася саме через таймаут».

---

### Додаткове 3. Відстеження прогресу через `IProgress<int>`
- **Команда в терміналі:**
  ```bash
  dotnet run --project src/Cli -f net10.0 --no-build -- --file --progress
  ```
  *(Вивід: у консолі біжать відсотки `Прогрес імпорту: 100%`, потім `Імпортовано записів: 17`)*
- **Що показати в коді:** 
  - У `src/Core/Services/CatalogService.cs` рядок з `progress?.Report(percent)`.
  - У `tests/Core.Tests/ImportTests.cs` тест `ImportAsync_ReportsProgressCorrectly`.
- **Що сказати:**
  > «Метод `ImportAsync` приймає параметр `IProgress<int>`. На кожній ітерації обчислюється відсоток `(i + 1) * 100 / total` і викликається `Report`. У CLI підписник малює відсотки в одному рядку через `\r`».

---

### Додаткове 4. Покриття коду тестами (Code Coverage)
- **Команда в терміналі:**
  ```bash
  dotnet test --collect:"XPlat Code Coverage"
  ```
- **Результат:** У папці `TestResults` створюється файл `coverage.cobertura.xml`.
- **Що сказати:**
  > «У тестовий проєкт підключено `coverlet.collector`. Команда з прапорцем `--collect` формує звіт покриття. Ключова бізнес-логіка Core (доменні сутності, сервіс, звіти, імпортер) має практично 100% покриття. Непокритими залишаються лише специфічні службові мапери, що є цілком прийнятним».

---

## 💡 Топ-3 підступних запитання викладача та короткі відповіді

1. **«Чи створює `await` новий потік?»**  
   — *«Ні! `await` відпускає поточний потік назад у пул потоків, а коли операція I/O завершується сигналом ОС, вільний потік із пулу продовжує виконання».*

2. **«Чому `OperationCanceledException` ловиться окремим catch перед `Exception`?»**  
   — *«Бо скасування — це штатне завершення на прохання користувача. Якщо ловити його в загальному catch, скасування виглядало б як аварія програми».*

3. **«Чому в тестах ви використовуєте `InMemoryCatalogStore`, а не файл?»**  
   — *«Щоб тести були швидкими (мікросекунди), повністю ізольованими один від одного та не залежали від прав доступу чи стану жорсткого диска».*
