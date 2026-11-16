# Посібник для захисту лабораторної роботи № 8

## Тема роботи

**Асинхронне введення-виведення, скасування операцій, доменні винятки та модульне тестування шару Core.**

Посібник відповідає восьми пунктам Definition of Done та чотирьом додатковим завданням зі скриншотів. Команди наведено для PowerShell у Windows. Перед запуском перейдіть у корінь репозиторію `C:\dev\CrossApp`.

## Підготовка та швидкий запуск

Збірка solution:

```powershell
dotnet build .\CrossApp.slnx
```

Запуск успішного імпорту:

```powershell
dotnet run --project .\src\Cli\Cli.csproj -f net10.0 --no-build -- --file
```

Очікуваний результат: `Імпортовано записів: 17`.

Запуск тестів проєкту `Core.Tests`:

```powershell
dotnet test .\tests\Core.Tests\Core.Tests.csproj
```

## Вступна промова

> «У лабораторній роботі № 8 я реалізував асинхронний імпорт товарів до каталогу складу. Файл читається асинхронно через `FileStream` з `useAsync: true` та API `System.Text.Json`, а не через блокуючий код у `Task.Run`. Для скасування операції використовується `CancellationToken`, який передається від CLI до сервісу й імпортера. Бізнес-помилки відокремлені від технічних за допомогою `DomainException`. Також реалізовано потоковий імпорт, передавання прогресу та модульні тести xUnit для шару Core».

## Блок 1. Definition of Done

### Пункт 1. Тестовий проєкт у solution і залежність лише від Core

**Що показати:** [CrossApp.slnx](./CrossApp.slnx) і [Core.Tests.csproj](./tests/Core.Tests/Core.Tests.csproj).

**Команди для перевірки:**

```powershell
dotnet sln .\CrossApp.slnx list
dotnet test .\tests\Core.Tests\Core.Tests.csproj
```

**Що сказати:**

> «Тести винесені в окремий проєкт `Core.Tests`, який доданий до solution. Він має посилання на `src/Core/Core.csproj`, але не залежить від CLI. Тому тести перевіряють бізнес-логіку ядра незалежно від консольного інтерфейсу».

### Пункт 2. Асинхронне читання без `Task.Run`

**Що показати:** [JsonCatalogImporter.cs](./src/Core/Storage/JsonCatalogImporter.cs), метод `LoadAsync`.

**Команда для демонстрації імпорту:**

```powershell
dotnet run --project .\src\Cli\Cli.csproj -f net10.0 --no-build -- --file
```

**Що сказати:**

> «Файл відкривається через `FileStream` із `useAsync: true`, а JSON читається за допомогою `JsonSerializer.DeserializeAsync`. Це асинхронне введення-виведення: під час очікування диска потік не блокується на синхронному читанні. `Task.Run` тут не потрібен, бо він лише переніс би блокуючу роботу на потік пулу».

### Пункт 3. Наскрізне скасування через `CancellationToken`

**Що показати:**

- [CatalogService.cs](./src/Core/Services/CatalogService.cs), методи `ImportAsync` та `ImportStreamAsync`;
- [JsonCatalogImporter.cs](./src/Core/Storage/JsonCatalogImporter.cs), передавання токена до десеріалізатора та перевірки у циклі.

**Команда для перевірки заздалегідь скасованої операції:**

```powershell
dotnet run --project .\src\Cli\Cli.csproj -f net10.0 --no-build -- --file --cancel
```

Очікуваний результат: `Імпорт скасовано.`

**Що сказати:**

> «Скасування в .NET кооперативне: код періодично перевіряє токен і завершує роботу, коли отримує запит на скасування. Токен передається від CLI до `CatalogService`, далі до імпортера та асинхронного десеріалізатора. У циклах викликається `ThrowIfCancellationRequested()`».

### Пункт 4. CLI обробляє Ctrl+C і таймаут без стектрейсу

**Що показати:** [Program.cs](./src/Cli/Program.cs), обробник `Console.CancelKeyPress`, налаштування таймауту та `catch (OperationCanceledException)`.

**Команди:**

```powershell
# Скасування одразу після запуску
dotnet run --project .\src\Cli\Cli.csproj -f net10.0 --no-build -- --file --cancel

# Скасування за таймаутом
dotnet run --project .\src\Cli\Cli.csproj -f net10.0 --no-build -- --file --timeout 1ms

# Для ручної демонстрації натисніть Ctrl+C під час імпорту
dotnet run --project .\src\Cli\Cli.csproj -f net10.0 --no-build -- --file
```

Очікуваний результат при скасуванні: `Імпорт скасовано.` без стектрейсу. Для ручної перевірки Ctrl+C можна використати великий JSON-файл, оскільки штатний каталог імпортується швидко.

**Що сказати:**

> «CLI перетворює Ctrl+C на запит скасування через `CancellationTokenSource`. Таймаут також скасовує цей токен. `OperationCanceledException` обробляється окремо від інших винятків, бо скасування є очікуваним завершенням операції, а не неочікуваним збоєм».

### Пункт 5. `DomainException` для помилок предметної області

**Що показати:**

- [DomainException.cs](./src/Core/DomainException.cs);
- [JsonCatalogImporter.cs](./src/Core/Storage/JsonCatalogImporter.cs), перевірку кількості та випадок, коли файл не містить списку;
- [Program.cs](./src/Cli/Program.cs), обробник `catch (DomainException ex)`.

**Команда для демонстрації некоректних даних:**

```powershell
dotnet run --project .\src\Cli\Cli.csproj -f net10.0 --no-build -- --file --path .\data\bad_sample.json
```

Очікуваний результат: повідомлення `Дані відхилено: ...` з описом помилки запису.

**Що сказати:**

> «`DomainException` позначає помилку даних або порушення правила предметної області, наприклад нульову кількість товару. Технічні проблеми читання файла не маскуються під доменні помилки. Це дає змогу CLI повідомити користувачу зрозумілу причину».

### Пункт 6. Модульні тести: інваріанти, події, звіти та скасування

**Що показати:**

- [CatalogServiceTests.cs](./tests/Core.Tests/CatalogServiceTests.cs);
- [ReportServiceTests.cs](./tests/Core.Tests/ReportServiceTests.cs);
- [ImportTests.cs](./tests/Core.Tests/ImportTests.cs).

**Команди:**

```powershell
# Запустити всі тести Core.Tests
dotnet test .\tests\Core.Tests\Core.Tests.csproj

# Запустити окремо тести імпорту
dotnet test .\tests\Core.Tests\Core.Tests.csproj --filter FullyQualifiedName~ImportTests
```

**Що сказати:**

> «Тести покривають різні типи поведінки: дотримання інваріантів сутності, подію зміни каталогу, формування звітів і сценарії імпорту. Також перевіряються некоректні дані, ідемпотентність, прогрес і скасування. Тестовий проєкт використовує xUnit, а для ізольованих перевірок сервісу можна застосовувати in-memory сховище».

Під час демонстрації орієнтуйтеся на підсумок саме щойно виконаної команди `dotnet test`, бо кількість тестів може змінюватися.

### Пункт 7. Немає `async void`, `.Result` та `.Wait()`

**Що показати:** асинхронні сигнатури в [CatalogService.cs](./src/Core/Services/CatalogService.cs) і [JsonCatalogImporter.cs](./src/Core/Storage/JsonCatalogImporter.cs).

**Команда для пошуку в основних проєктах:**

```powershell
Get-ChildItem .\src, .\tests -Recurse -Filter *.cs |
    Select-String -Pattern 'async void|\.Result\b|\.Wait\('
```

Якщо команда нічого не виводить, збігів у цих файлах немає.

**Що сказати:**

> «Асинхронні методи повертають `Task` або асинхронний потік. Виклики очікуються через `await`, без синхронного блокування через `.Result` чи `.Wait()`. Це спрощує поширення винятків і допомагає уникати блокувань під час очікування».

### Пункт 8. Документація і запуск імпорту

**Що показати:** [README.md](./README.md) і цей посібник [LAB08_DEFENSE_GUIDE.md](./LAB08_DEFENSE_GUIDE.md).

**Команди:**

```powershell
dotnet build .\CrossApp.slnx
dotnet run --project .\src\Cli\Cli.csproj -f net10.0 --no-build -- --file
```

**Що сказати:**

> «У README описані призначення проєкту та команди запуску основних сценаріїв. З цього посібника можна послідовно показати виконання вимог і перевірити результат кожної демонстрації».

## Сценарій усного захисту з командами

Нижче — послідовний варіант виступу. Команди розраховані на PowerShell, відкритий у корені репозиторію `C:\dev\CrossApp`. Спочатку зберіть solution, щоб наступні команди з `--no-build` запускали актуальний код.

### 1. Вступ і збірка проєкту

**Сказати:**

> «Тема моєї лабораторної роботи — асинхронний імпорт даних, скасування операцій, доменні винятки та модульне тестування. Проєкт моделює каталог товарів складу. Спочатку зберу solution, а потім покажу основні сценарії роботи».

**Запустити:**

```powershell
dotnet build .\CrossApp.slnx
```

### 2. Продемонструвати звичайний імпорт

**Сказати:**

> «CLI запускає імпорт із JSON-файла. За замовчуванням дані беруться з каталогу проєкту, а після завершення програма показує кількість імпортованих записів».

**Запустити:**

```powershell
dotnet run --project .\src\Cli\Cli.csproj -f net10.0 --no-build -- --file
```

Очікується повідомлення про імпорт 17 записів.

### 3. Пояснити асинхронне читання і потоковий варіант

**Сказати:**

> «У `JsonCatalogImporter` файл відкривається асинхронним `FileStream`, а JSON читається через `System.Text.Json`. Для потокового сценарію використовується `IAsyncEnumerable<Goods>`: імпортер видає товари поступово, а сервіс обробляє їх через `await foreach`. Це дає змогу не завантажувати весь список товарів перед початком обробки».

**Показати:** [JsonCatalogImporter.cs](./src/Core/Storage/JsonCatalogImporter.cs), [ICatalogImporter.cs](./src/Core/Abstractions/ICatalogImporter.cs) та [CatalogService.cs](./src/Core/Services/CatalogService.cs).

**Запустити потоковий імпорт:**

```powershell
dotnet run --project .\src\Cli\Cli.csproj -f net10.0 --no-build -- --file --stream
```

### 4. Показати скасування і таймаут

**Сказати:**

> «Скасування в .NET кооперативне. CLI передає `CancellationToken` у сервіс та імпортер, а код перевіряє токен і може коректно зупинити роботу. Ctrl+C і таймаут перетворюються на запит скасування. CLI окремо обробляє `OperationCanceledException`, тому користувач бачить повідомлення, а не стектрейс».

**Показати:** [Program.cs](./src/Cli/Program.cs), [CatalogService.cs](./src/Core/Services/CatalogService.cs) та [JsonCatalogImporter.cs](./src/Core/Storage/JsonCatalogImporter.cs).

**Перевірити скасування прапорцем і таймаутом:**

```powershell
dotnet run --project .\src\Cli\Cli.csproj -f net10.0 --no-build -- --file --cancel
dotnet run --project .\src\Cli\Cli.csproj -f net10.0 --no-build -- --file --timeout 1ms
```

Очікується повідомлення `Імпорт скасовано.`. Для демонстрації Ctrl+C запустіть звичайний імпорт і натисніть `Ctrl+C` під час виконання; невеликий файл може імпортуватися швидше, ніж вдасться перервати його вручну.

### 5. Показати доменну помилку

**Сказати:**

> «Для помилок предметної області є окремий `DomainException`. Наприклад, імпортер відхиляє товар із нульовою кількістю. Технічна помилка на кшталт відсутнього файла має інший тип і обробляється окремо».

**Показати:** [DomainException.cs](./src/Core/DomainException.cs), перевірку в [JsonCatalogImporter.cs](./src/Core/Storage/JsonCatalogImporter.cs) та обробник у [Program.cs](./src/Cli/Program.cs).

**Запустити:**

```powershell
dotnet run --project .\src\Cli\Cli.csproj -f net10.0 --no-build -- --file --path .\data\bad_sample.json
```

Очікується повідомлення `Дані відхилено: ...` з поясненням некоректних даних.

### 6. Показати прогрес імпорту

**Сказати:**

> «Сервіс приймає `IProgress<int>` і повідомляє про відсоток виконання, не залежачи від того, як саме цей прогрес буде показаний. CLI підписується на повідомлення й виводить відсотки у консолі».

**Запустити:**

```powershell
dotnet run --project .\src\Cli\Cli.csproj -f net10.0 --no-build -- --file --progress
```

Для цього прикладу не додавайте `--stream`: потоковий імпорт передає лічильник доданих товарів, тоді як звичайний імпорт повідомляє відсотки.

### 7. Запустити тести

**Сказати:**

> «Тести винесені в окремий xUnit-проєкт `Core.Tests`, який посилається на Core, а не на CLI. Перевіряються бізнес-інваріанти, події, звіти, імпорт, помилки та скасування. Тестовий запуск дає змогу повторно перевірити поведінку без ручної взаємодії з CLI».

**Показати:** [Core.Tests.csproj](./tests/Core.Tests/Core.Tests.csproj), [CatalogServiceTests.cs](./tests/Core.Tests/CatalogServiceTests.cs), [ReportServiceTests.cs](./tests/Core.Tests/ReportServiceTests.cs) та [ImportTests.cs](./tests/Core.Tests/ImportTests.cs).

**Запустити всі тести:**

```powershell
dotnet test .\tests\Core.Tests\Core.Tests.csproj
```

**За потреби показати тест linked token окремо:**

```powershell
dotnet test .\tests\Core.Tests\Core.Tests.csproj --filter FullyQualifiedName~Import_WithLinkedTokenSource_ProvesTimeoutTriggered
```

### 8. Зібрати звіт покриття і завершити виступ

**Сказати:**

> «Додатково можна зібрати звіт покриття коду. Він показує, які ділянки виконувалися під час тестів, але сам по собі не доводить відсутність помилок — важливі також зміст і якість тестових перевірок».

**Запустити:**

```powershell
dotnet test .\tests\Core.Tests\Core.Tests.csproj --collect:"XPlat Code Coverage"
Get-ChildItem .\tests\Core.Tests\TestResults -Recurse -Filter coverage.cobertura.xml
```

**Завершити:**

> «Отже, у роботі є звичайний і потоковий асинхронний імпорт, наскрізне скасування, обробка доменних помилок, повідомлення про прогрес і тести Core. Основні сценарії можна перевірити командами, наведеними в посібнику та README».

## Блок 2. Додаткові завдання

### Додаткове 1. Потоковий імпорт через `IAsyncEnumerable<Goods>`

**Що показати:**

- [ICatalogImporter.cs](./src/Core/Abstractions/ICatalogImporter.cs), сигнатуру `ReadStreamAsync`;
- [JsonCatalogImporter.cs](./src/Core/Storage/JsonCatalogImporter.cs), `DeserializeAsyncEnumerable`, `[EnumeratorCancellation]` і `yield return`;
- [CatalogService.cs](./src/Core/Services/CatalogService.cs), `await foreach` у `ImportStreamAsync`.

**Команда:**

```powershell
dotnet run --project .\src\Cli\Cli.csproj -f net10.0 --no-build -- --file --stream
```

**Що сказати:**

> «Звичайний імпортер спочатку читає список, а потоковий імпортер видає товари по одному через `IAsyncEnumerable<Goods>`. Сервіс отримує їх за допомогою `await foreach`. Це дає змогу обробляти записи поступово, не формуючи весь список товарів у пам’яті. Атрибут `[EnumeratorCancellation]` пов’язує токен виклику з асинхронним перерахуванням».

### Додаткове 2. Зв’язані токени користувача й таймауту

**Що показати:** тест `Import_WithLinkedTokenSource_ProvesTimeoutTriggered` у [ImportTests.cs](./tests/Core.Tests/ImportTests.cs).

**Команда для запуску цього тесту:**

```powershell
dotnet test .\tests\Core.Tests\Core.Tests.csproj --filter FullyQualifiedName~Import_WithLinkedTokenSource_ProvesTimeoutTriggered
```

**Що сказати:**

> «`CreateLinkedTokenSource` створює токен, який скасовується, якщо скасовано хоча б один із початкових токенів. У тесті спливає таймаут: токен таймауту та зв’язаний токен скасовуються, а користувацький токен залишається активним. Так перевіряється, що причиною скасування був саме таймаут».

### Додаткове 3. Прогрес через `IProgress<int>`

**Що показати:**

- [CatalogService.cs](./src/Core/Services/CatalogService.cs), обчислення відсотка та виклик `progress.Report`;
- [Program.cs](./src/Cli/Program.cs), підписник, який виводить прогрес;
- [ImportTests.cs](./tests/Core.Tests/ImportTests.cs), тест `ImportAsync_ReportsProgressCorrectly`.

**Команди:**

```powershell
# Імпорт із відображенням відсотків
dotnet run --project .\src\Cli\Cli.csproj -f net10.0 --no-build -- --file --progress

# Перевірка тестом
dotnet test .\tests\Core.Tests\Core.Tests.csproj --filter FullyQualifiedName~ImportAsync_ReportsProgressCorrectly
```

**Що сказати:**

> «`IProgress<int>` дає сервісу змогу повідомляти про хід операції, не прив’язуючи Core до консолі. У звичайному `ImportAsync` передається відсоток оброблених записів. CLI підписується на ці значення і відображає їх користувачу».

Для демонстрації саме відсотків запускайте без `--stream`: у потоковому сценарії сервіс передає лічильник доданих товарів.

### Додаткове 4. Code Coverage

**Що показати:** пакет `coverlet.collector` у [Core.Tests.csproj](./tests/Core.Tests/Core.Tests.csproj) та звіт, створений після виконання тестів.

**Команда:**

```powershell
dotnet test .\tests\Core.Tests\Core.Tests.csproj --collect:"XPlat Code Coverage"
```

Знайти створений звіт можна так:

```powershell
Get-ChildItem .\tests\Core.Tests\TestResults -Recurse -Filter coverage.cobertura.xml
```

**Що сказати:**

> «Збір покриття показує, які рядки та гілки коду виконалися під час тестів. Звіт допомагає побачити непокриті ділянки Core і вирішити, чи потрібні для них додаткові сценарії. Сам відсоток покриття не доводить відсутність помилок, тому його потрібно розглядати разом із якістю перевірок».

## Запитання викладача

**Чи створює `await` окремий потік?**

> «Не обов’язково. `await` не створює новий потік сам по собі. Під час очікування асинхронного I/O потік не блокується, а продовження виконується після завершення операції».

**Чому `OperationCanceledException` обробляється окремо?**

> «Скасування є очікуваним сценарієм, тому його потрібно відрізняти від помилки. CLI виводить коротке повідомлення замість загального повідомлення про збій».

**Чим `DomainException` відрізняється від `FileNotFoundException`?**

> «`DomainException` повідомляє про некоректні дані або порушення бізнес-правила. `FileNotFoundException` повідомляє, що інфраструктура не знайшла файл. Це різні причини, тому їх обробляють окремо».

**Навіщо тестам in-memory сховище?**

> «Воно дає змогу перевірити сервіс без залежності від файлової системи. Такі тести ізольовані та передбачувані; файлові сценарії імпорту окремо створюють тимчасові файли й очищають їх після перевірки».

**Чим потоковий імпорт відрізняється від звичайного?**

> «Звичайний імпорт завантажує колекцію перед обробкою, а потоковий видає записи поступово. Потоковий підхід зручніший для великих обсягів, хоча не скасовує потреби контролювати помилки та скасування».

## Коротка завершальна відповідь

> «У роботі реалізовано асинхронне читання JSON, кооперативне скасування через `CancellationToken`, обробку бізнес-помилок через `DomainException`, потоковий імпорт і передавання прогресу. Поведінку Core перевіряють xUnit-тести, а покриття можна зібрати через Coverlet. Запуск і перевірка кожного сценарію документовані командами в цьому посібнику та README».
