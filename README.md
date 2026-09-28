# CrossApp

Наскрізний проєкт з крос-платформного програмування.

Предметна область: **Warehouse**. Сутності: Goods, Batches, Balances, Transfers.
Призначення: облік залишків товарів по партіях.

## Структура solution

```
CrossApp/
├── CrossApp.slnx
├── README.md
├── .gitignore
├── data/
│   ├── sample.csv           (10+ рядків, 2-3 навмисно пошкоджені)
│   └── sample.json          (додаткове завдання — JSON-імпорт)
└── src/
    ├── Core/
    │   ├── Core.csproj          (TargetFrameworks: net8.0;net10.0)
    │   ├── EnvironmentInfo.cs   (record EnvironmentReport + клас EnvironmentInfo)
    │   ├── Dto/
    │   │   ├── GoodsDto.cs      (record — товар)
    │   │   ├── BatchDto.cs      (record — партія товару)
    │   │   └── ImportResult.cs  (record ImportResult<T>)
    │   └── Import/
    │       ├── GoodsCsvImporter.cs   (Load + ParseLine, pattern matching)
    │       └── GoodsJsonImporter.cs  (додаткове завдання)
    └── Cli/
        ├── Cli.csproj           (TargetFramework: net10.0; ProjectReference на Core)
        └── Program.cs
```

Проєкт **Core** — бібліотека класів (class library) без точки входу. Містить:
- допоміжний код для отримання інформації про середовище виконання: `EnvironmentReport`
  (record) та `EnvironmentInfo` (static class з методом `Collect()`);
- доменні DTO (`Core/Dto`) — `GoodsDto`, `BatchDto`, `ImportResult<T>`;
- імпортери (`Core/Import`) — `GoodsCsvImporter` і `GoodsJsonImporter`, які розбирають
  вхідний файл і повертають `ImportResult<GoodsDto>` (успішні записи разом з переліком
  помилок, а не виняток на першому пошкодженому рядку).

Наступними лабораторними сюди додадуться сутності з поведінкою (`Core/Domain`) та
сховища (`Core/Storage`).

Проєкт **Cli** — консольна точка входу. `Program.cs`:
1. визначає шлях до файлу (`args[0]` або `data/sample.csv` за замовчуванням) і перевіряє
   його існування;
2. обирає імпортер за розширенням файлу (`.csv` → `GoodsCsvImporter`, `.json` →
   `GoodsJsonImporter`) і виводить кількість завантажених записів, перші 5 з них та
   перелік помилок з номерами рядків;
3. виводить інформацію про середовище виконання (`EnvironmentInfo.Collect()`) текстом
   або JSON за прапорцем `--json`.

Жодної логіки розбору файлів чи визначення середовища в `Program.cs` немає — вся вона
в `Core`. Залежність односторонняя: `Cli → Core`.

## Середовище розробки

.NET SDK 10.0, Windows 10 x64.

## Збірка і запуск

```bash
dotnet build
dotnet run --project src/Cli
```

## Формат вхідних даних

**CSV** (`data/sample.csv`) — роздільник `;`, перший рядок — заголовок
(`id;sku;name;unit;quantity`), кодування UTF-8. Розбір рядка — через `switch expression`
з патернами: перевірка кількості колонок, порожніх значень, коректності числа (`TryParse`
+ `when`). Пошкоджені рядки не перериває імпорт — потрапляють у список помилок з номером
рядка.

**JSON** (`data/sample.json`, додаткове завдання) — масив об'єктів з тими самими полями,
що й `GoodsDto` (`PropertyNameCaseInsensitive = true`). На відміну від CSV-імпортера,
пошкоджений JSON не парситься частково — помилка (якщо файл невалідний) зупиняє весь
імпорт, а не окремий рядок.

## Запуск імпорту

```bash
# CSV за замовчуванням (data/sample.csv)
dotnet run --project src/Cli

# CSV, явний шлях
dotnet run --project src/Cli -- data/sample.csv

# JSON (додаткове завдання) — обирається автоматично за розширенням
dotnet run --project src/Cli -- data/sample.json

# Неіснуючий файл — коректне повідомлення, без винятку
dotnet run --project src/Cli -- no-such-file.csv
```

Вивід результату у форматі JSON для звіту про середовище виконання (не плутати з
JSON-імпортом даних):

```bash
dotnet run --project src/Cli -- --json
```

Перевірити, що бібліотека Core збирається окремо:

```bash
dotnet build src/Core/Core.csproj
```

Зібрати під конкретний TFM (Core підтримує обидва, Cli — лише net10.0):

```bash
dotnet build -f net10.0
dotnet build -f net8.0     # успішно лише для Core
```

## Публікація (publish)

Self-contained (включає копію .NET runtime, запуск без встановленого .NET):

```bash
dotnet publish src/Cli -c Release -r win-x64 --self-contained true
```

Framework-dependent (лише код застосунку і залежності, потребує встановленого .NET 10 Runtime
на цільовій машині):

```bash
dotnet publish src/Cli -c Release -r win-x64 --self-contained false
```

> Якщо публікуєте обидва варіанти для одного й того самого RID — вказуйте різні вихідні
> каталоги (`-o`), інакше другий publish перезапише перший:
> `dotnet publish src/Cli -c Release -r win-x64 --self-contained true -o publish/sc-win-x64`

Додаткові параметри (опційно):

```bash
# Об'єднати все в один виконуваний файл
dotnet publish src/Cli -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

# Видалити невикористаний код (зменшує розмір, ризиковано з рефлексією)
dotnet publish src/Cli -c Release -r win-x64 --self-contained true -p:PublishTrimmed=true
```

Запуск опублікованого застосунку — напряму з каталогу publish, а не через `dotnet run`:

```bash
cd src/Cli/bin/Release/net10.0/win-x64/publish
./Cli.exe
```

## Порівняння режимів публікації (RID win-x64)

| RID     | Режим                              | Розмір publish     | Потрібен встановлений runtime? |
|---------|-------------------------------------|---------------------|----------------------------------|
| win-x64 | self-contained (single-file)        | ≈ 71 МБ             | ні                               |
| win-x64 | self-contained + PublishTrimmed     | ≈ 20 МБ             | ні                               |
| win-x64 | framework-dependent                 | ≈ 0,24 МБ (241 КБ)  | так (.NET 10 Runtime)            |

**Self-contained** — до каталогу publish входять код застосунку, NuGet-залежності та копія
.NET runtime для вказаного RID. Застосунок можна перенести на комп'ютер без встановленого
.NET і запустити напряму, але каталог значно більший і прив'язаний до конкретної платформи.

**Framework-dependent** — каталог містить лише код застосунку і залежності, без runtime.
Розмір мінімальний, але на цільовій машині обов'язково має бути встановлений сумісний
.NET 10 Runtime.

`PublishSingleFile` об'єднує всі компоненти в один виконуваний файл (замість десятків
окремих `.dll`). `PublishTrimmed` додатково видаляє невикористаний код і зменшує розмір
(тут — приблизно у 3,5 раза), однак може конфліктувати з кодом, що покладається на
рефлексію (наприклад, `System.Text.Json.JsonSerializer` за замовчуванням) — компілятор
позначає це попередженням `IL2026`.

## Multi-targeting

`src/Core/Core.csproj` збирається для двох цільових платформ:

```xml
<TargetFrameworks>net8.0;net10.0</TargetFrameworks>
```

`src/Cli/Cli.csproj` навмисно лишено з одним `<TargetFramework>net10.0</TargetFramework>`,
оскільки multi-targeting потрібен саме бібліотеці, а не точці входу.
