# CrossApp — Warehouse

Навчальний проєкт з курсу кросплатформної розробки (.NET 10). **Домен:** склад будівельних матеріалів, сутність `Goods` (id, sku, name, unit, quantity) + партії `Batch`.

**Поточний етап — лабораторна робота 9:** шляхи без жорстких літералів, конфігурація з `appsettings.json` із перевизначенням змінними середовища, `user-secrets`, імпорт/експорт усієї колекції в JSON. Функціонал лаб. 1–8 (у тому числі всі додаткові завдання лаб. 8) збережено.

---

## Структура репозиторію

```
CrossApp/
├── CrossApp.slnx
├── README.md
├── data/                     вхідні приклади (catalog.json, sample.*, bad_sample.json, *.csv) — у git
├── docs/                     звіти й матеріали до захисту (по папках lab04 … lab09)
├── scripts/
│   ├── lab09-demo.sh         повний сценарій перевірки ЛР9 для Git Bash
│   └── tidy-structure.sh     одноразове прибирання зайвих/дублюючих файлів
├── src/
│   ├── Core/                 доменна модель, сервіси, звіти, сховища (без залежності від Cli)
│   │   ├── Abstractions/     ICatalogStore, ICatalogImporter, INotificationSink
│   │   ├── Configuration/    AppOptions, StorageOptions, LoggingOptions, WarehouseOptions   ← лаб. 9
│   │   ├── Domain/ Dto/ Events/ Import/ Reports/ Services/
│   │   ├── Storage/          FileCatalogStore, InMemory…, Caching…, JsonCatalogImporter, CatalogJson ← лаб. 9
│   │   └── DomainException.cs
│   └── Cli/                  точка входу
│       ├── appsettings.json, appsettings.Development.json, appsettings.Production.json         ← лаб. 9
│       ├── Configuration/    AppConfiguration (збирання джерел конфігурації)                   ← лаб. 9
│       ├── Logging/          ConsoleLog (рівень з Logging:MinimumLevel)
│       ├── Commands/         ImportCommand, DataCommands, ReportCommands, HelpText
│       ├── Composition/      StoreFactory, StoreOptions (лаб. 5)
│       ├── Notifications/    ConsoleSink, FileSink (лаб. 6)
│       └── Program.cs        розбір аргументів + диспетчеризація команд
└── tests/
    ├── Core.Tests/           xUnit (посилається лише на Core)
    └── DomainChecks, ServiceChecks, EventChecks, ReportChecks   консольні перевірки лаб. 4–7
```

Пакети конфігурації (`Microsoft.Extensions.Configuration.*`) підключено лише в `src/Cli`: домен нічого не знає про JSON-файли та user-secrets.

---

## Швидкий старт (Git Bash)

```bash
cd /c/dev/CrossApp
dotnet build CrossApp.slnx
dotnet test tests/Core.Tests

# Лаб. 9
dotnet run --project src/Cli -- config            # активна конфігурація
dotnet run --project src/Cli -- export            # записати всю колекцію в JSON (файл даних із конфігурації)
dotnet run --project src/Cli -- list              # показати файл даних
dotnet run --project src/Cli -- import            # асинхронний імпорт у файл даних
dotnet run --project src/Cli -- add SKU-500 "Гвинт" 25     # одиниця — Warehouse:DefaultUnit
```

Повний сценарій перевірки однією командою: `bash scripts/lab09-demo.sh`.

> Одну зібрану програму можна запускати швидше: `dotnet build` один раз, далі `dotnet run --no-build --project src/Cli -- <команда>`.

---

## Лабораторна 9: конфігурація, шляхи, JSON, secrets

### Профілі

Профіль вибирається змінною `CROSSAPP_ENVIRONMENT` (за замовчуванням `Development`) і підключає `appsettings.{профіль}.json` поверх `appsettings.json`.

| Профіль | Файл | Логування (`Logging:MinimumLevel`) | Де лежать дані |
| --- | --- | --- | --- |
| *(база)* | `appsettings.json` | `Information` | `data/items.json` поруч із програмою (`AppContext.BaseDirectory`) |
| `Development` | `appsettings.Development.json` | `Debug` — детальний журнал | як у базі: `src/Cli/bin/Debug/net10.0/data/items.json` |
| `Production` | `appsettings.Production.json` | `Warning` | `Storage:Location = LocalApplicationData` → `%LOCALAPPDATA%\CrossApp\data\items.json` |

### Таблиця ключів

| Ключ | Значення за замовчуванням | Призначення |
| --- | --- | --- |
| `EnvironmentName` | `Development` | назва середовища (також видно в `config`) |
| `Logging:MinimumLevel` | `Information` | `Trace` / `Debug` / `Information` / `Warning` / `Error` |
| `Storage:DataPath` | `data` | каталог даних: відносний (від бази, див. `Location`) або абсолютний |
| `Storage:FileName` | `items.json` | ім'я файлу даних (без каталогів) |
| `Storage:Location` | `BaseDirectory` | `BaseDirectory` або `LocalApplicationData` — від чого відраховується відносний `DataPath` |
| `Warehouse:DefaultUnit` | `шт` | одиниця виміру для `add`, якщо її не вказано |
| `Integration:ApiKey` | — | **секрет**: лише user-secrets або змінна середовища; друкується тільки «присутній, довжина N» |

### Порядок джерел (пізніше перекриває раніше)

```
appsettings.json → appsettings.{профіль}.json → user-secrets → змінні CROSSAPP_* → аргументи командного рядка
```

Додатково проста змінна `CROSSAPP_DATA_PATH` перекриває `Storage:DataPath` (застосовується після збирання конфігурації).

### Перевизначення значень

Вкладений ключ у змінній середовища записується з подвійним підкресленням: `Storage:DataPath` → `CROSSAPP_Storage__DataPath`.

**bash (Git Bash)** — змінна діє на одну команду:

```bash
CROSSAPP_DATA_PATH=C:/temp/crossapp-data dotnet run --project src/Cli -- export
CROSSAPP_Storage__FileName=my.json dotnet run --project src/Cli -- config
CROSSAPP_ENVIRONMENT=Production dotnet run --project src/Cli -- config
dotnet run --project src/Cli -- config --Storage:DataPath=C:/temp/cli-data     # аргумент командного рядка
```

**PowerShell** — змінна діє в поточному сеансі:

```powershell
$env:CROSSAPP_DATA_PATH = "C:\temp\crossapp-data"
dotnet run --project src/Cli -- export
$env:CROSSAPP_ENVIRONMENT = "Production"
Remove-Item Env:CROSSAPP_DATA_PATH, Env:CROSSAPP_ENVIRONMENT      # скинути
```

### Secret (`Integration:ApiKey`)

У репозиторії є лише `UserSecretsId` у `src/Cli/Cli.csproj`; значення зберігається поза репозиторієм.

```bash
dotnet user-secrets set "Integration:ApiKey" "local-dev-key" --project src/Cli
dotnet user-secrets list --project src/Cli
dotnet run --project src/Cli -- config            # ApiKey: присутній, довжина 13
git status --short                                # після set — без змін

# або змінною середовища (наприклад, на CI), вона перекриває user-secrets:
CROSSAPP_Integration__ApiKey=from-env dotnet run --project src/Cli -- config
```

| ОС | Де лежить `secrets.json` |
| --- | --- |
| Windows | `%APPDATA%\Microsoft\UserSecrets\<UserSecretsId>\secrets.json` |
| Linux / macOS | `~/.microsoft/usersecrets/<UserSecretsId>/secrets.json` |

`user-secrets` — лише для локальної розробки (відкритий JSON у профілі користувача, без шифрування); для розгорнутого застосунку — змінні середовища або сховище секретів.

### Шляхи без жорстких літералів

- Усі шляхи будуються через `Path.Combine` / `Path.GetFullPath`, а не конкатенацією рядків із `\` чи `/` — роздільник підставляє платформа.
- Базовий каталог — `AppContext.BaseDirectory`, а не поточний робочий каталог. Тому запуск із кореня solution й із `src/Cli` дає **той самий** файл даних.
- Перевірка: `git grep -nE 'C:\\|/home/' -- src` має бути порожнім.

### JSON-формат і атомарний запис

- `CatalogJson` (Core/Storage) — єдиний опис формату: camelCase, відступи, кирилиця читабельна (`UnsafeRelaxedJsonEscaping`, не `\u0426`), `null`-поля не записуються, читання нечутливе до регістру (старі PascalCase-файли читаються).
- Запис усієї колекції атомарний: тимчасовий файл `.{ім'я}.{guid}.tmp` у тому самому каталозі + `File.Move(..., overwrite: true)`. Читач бачить або старий, або новий файл повністю.
- Зіпсований JSON → `InvalidDataException` із назвою файлу (без stack trace у консолі).

### Команди CLI

| Команда | Що робить |
| --- | --- |
| `export [--out <файл>]` | записує всю колекцію в JSON; якщо файл даних порожній — експортує демонстраційний `SampleData` |
| `import [--path <файл>]` | асинхронно читає JSON і додає записи у файл даних (ідемпотентно); `--stream`, `--progress`, `--cancel`, `--timeout <ms/s>` |
| `list` | виводить вміст файлу даних |
| `add <sku> <назва> <к-сть> [одиниця]` | додає товар у файл даних |
| `config` | друкує активну конфігурацію (секрет — лише факт наявності) |

Без `--path` команда `import` читає файл даних (якщо він є), інакше вбудований `data/catalog.json`. Файл, створений `export`, можна імпортувати назад: у лаб. 9 нульовий залишок (наприклад, G-018) допустимий, тоді як `--file` (лаб. 8) і далі відхиляє записи з кількістю 0 через `DomainException`.

### Перевірка переносимості

```bash
cd /c/dev/CrossApp && dotnet run --project src/Cli -- config | grep "Файл даних"
cd /c/dev/CrossApp/src/Cli && dotnet run -- config | grep "Файл даних"      # той самий шлях
```

---

## Лабораторна 8: async/await, скасування, DomainException, тести (збережено)

```bash
dotnet run --project src/Cli -- --file                      # асинхронний імпорт у пам'ять: 17 записів
dotnet run --project src/Cli -- --file --cancel             # «Імпорт скасовано.»
dotnet run --project src/Cli -- --file --timeout 1ms        # скасування за таймаутом
dotnet run --project src/Cli -- --file --path data/bad_sample.json   # «Дані відхилено: Запис 1: кількість не може бути 0.»
dotnet run --project src/Cli -- --file --path missing.json  # «Файл не знайдено: …»
dotnet run --project src/Cli -- --file --stream             # додаткове 1: IAsyncEnumerable + [EnumeratorCancellation]
dotnet run --project src/Cli -- --file --progress           # додаткове 3: IProgress<int>
dotnet test tests/Core.Tests                                # unit-тести
dotnet test tests/Core.Tests --collect:"XPlat Code Coverage"  # додаткове 4: покриття
```

Додаткові завдання лаб. 8: **1)** потоковий імпорт (`JsonCatalogImporter.ReadStreamAsync`, `CatalogService.ImportStreamAsync`); **2)** зв'язані токени (`CreateLinkedTokenSource`, тест `Import_WithLinkedTokenSource_ProvesTimeoutTriggered`); **3)** `IProgress<int>`; **4)** покриття через `coverlet.collector`.
`DomainException` — порушення бізнес-правил (наприклад, кількість 0, пошкоджений запис), `ArgumentException` — некоректні аргументи, `IOException`/`JsonException` — технічні помилки, які не маскуються.

## Лабораторні 5–7 (збережено)

```bash
dotnet run --project src/Cli -- report-top                  # топ-5 за залишком
dotnet run --project src/Cli -- report-low | report-groups | report-sum
dotnet run --project src/Cli -- report-batches | report-top-batches | report-benchmark
dotnet run --project src/Cli -- report-top --cache --factory --list-only   # параметри сховища лаб. 5 (див. --help)
```

> Прапорець `--file` у будь-якій команді запускає асинхронний імпорт лаб. 8 (так було й раніше); файл даних лаб. 9 доступний командами `export`, `import`, `list`, `add`.

---

## Тести

```bash
dotnet test tests/Core.Tests                                  # 36 тестів xUnit (лаб. 8 — 16, лаб. 9 — 20)
dotnet run --project tests/DomainChecks                       # 56 перевірок (лаб. 4)
dotnet run --project tests/ServiceChecks                      # 44 перевірки (лаб. 5)
dotnet run --project tests/EventChecks                        # 34 перевірки (лаб. 6)
dotnet run --project tests/ReportChecks                       # 9 перевірок (лаб. 7)
```

Нові тести лаб. 9: `StorageOptionsTests` (відносний/абсолютний шлях, `LocalApplicationData`, захист від `FileName` з каталогами), `ConfigurationLayeringTests` (пізніше джерело перекриває раніше, `__` у змінних середовища, секрет за ключем), `FileCatalogStoreTests` (camelCase, кирилиця без `\uXXXX`, відсутність `.tmp`, зіпсований JSON, сумісність зі старим PascalCase-форматом), `CollectionRoundTripTests` (експорт → імпорт власного файлу з нульовим залишком; `--file` лаб. 8 лишається суворим).

---

## Definition of Done — лаб. 9

| Пункт | Стан | Де |
| --- | :---: | --- |
| Експорт і імпорт колекції в JSON | ✅ | `export`, `import`, `CatalogJson` |
| Немає абсолютних шляхів і паролів у коді | ✅ | `git grep -nE 'C:\\|/home/' -- src` порожній |
| `appsettings.json` у репозиторії, без secrets | ✅ | `src/Cli/appsettings*.json` |
| `CROSSAPP_DATA_PATH` перекриває шлях із файлу | ✅ | `AppConfiguration.Load` |
| Secret із user-secrets або env; запуск з довільного cwd | ✅ | `config`, `AppContext.BaseDirectory` |
| `dotnet build` і `dotnet test` зелені; коміт `lab09` | ✅ | див. «Коміт» нижче |

## Коміт

```bash
bash scripts/tidy-structure.sh      # один раз: прибрати дублікати, розкласти docs по папках
git add -A
git status --short                  # переконатися, що secrets.json / items.json не потрапили
git commit -m "lab09: appsettings, user-secrets, CROSSAPP_* env, JSON import/export, atomic write"
```

Звіт і відповіді на питання для захисту: `docs/lab09/lab09-report.md`.
