# CrossApp — лабораторна робота 7

## LINQ to Objects: фільтрація, сортування, групування, top-N та агрегати

Тема проєкту — склад Warehouse, доменна сутність — `Goods`. Звіти реалізовані в `Core/Reports`, а CLI тільки вибирає команду та друкує готові DTO. У проєкті використано лише LINQ to Objects над `IEnumerable<Goods>`: без `AsParallel`, Entity Framework або LINQ to SQL.

## Запуск у Git Bash

```bash
cd /c/dev/CrossApp
dotnet build CrossApp.slnx
dotnet run --project src/Cli -f net10.0 --no-build -- report-top
dotnet run --project src/Cli -f net10.0 --no-build -- report-low
dotnet run --project src/Cli -f net10.0 --no-build -- report-groups
dotnet run --project src/Cli -f net10.0 --no-build -- report-sum
dotnet run --project src/Cli -f net10.0 --no-build -- report-batches
dotnet run --project src/Cli -f net10.0 --no-build -- report-top-batches
dotnet run --project src/Cli -f net10.0 --no-build -- report-benchmark
dotnet run --project tests/ReportChecks -f net10.0 --no-build
```

Без команди запускається `report-top`. Для довідки: `dotnet run --project src/Cli -f net10.0 --no-build -- --help`.

Команди можна поєднати з режимами попереднього сховища, наприклад:

```bash
dotnet run --project src/Cli -f net10.0 --no-build -- report-groups --file --path data/catalog.json --cache --factory
```

`--list-only` лише показує дані й нічого не змінює.

## Звіти в Core

| Метод | Питання, на яке відповідає | LINQ-оператори | Результат |
| --- | --- | --- | --- |
| `LowStock(source, 10)` | Яких товарів залишилося менше 10? | `Where`, `OrderBy`, `ThenBy`, `Select` | `IReadOnlyList<StockLine>` |
| `TopByQuantity(source, 5)` | Які 5 товарів мають найбільший залишок? | `OrderBy`, `ThenBy`, власний `TopBy`, `Select` | `IReadOnlyList<StockLine>` |
| `ByUnit(source)` | Скільки позицій та одиниць у кожній групі? | query syntax: `group ... by ... into`, `Sum`, `Count`, `Select` | `IReadOnlyList<GroupSummary>` |
| `Totals(source)` | Скільки товарів, одиниць, яке середнє й максимум? | `ToList`, `Sum`, `Average`, `Max` | `StockTotals` |

Кожен метод приймає `IEnumerable<Goods>` і повертає DTO, а не доменний `Goods`. Методи не друкують у консоль і не змінюють джерело. `LowStock`, `TopByQuantity` і `ByUnit` матеріалізують результат у незмінний `IReadOnlyList`.

### Детермінований top-N

Спочатку `TopByQuantity` упорядковує джерело за `Sku`, а далі за `Id`. Власний extension-метод `TopBy` стабільно сортує за залишком за спаданням, тобто при однаковому залишку зберігає вже визначений порядок. Тому два запуски дають однаковий порядок рядків.

### Порожнє джерело

Для порожнього джерела спискові звіти повертають порожній список. `Totals` повертає `0, 0, 0.00, 0`, отже не викликає `Average` і `Max` для порожньої послідовності.

### Групування

```csharp
var query =
    from item in source
    group item by item.Unit.ToUpperInvariant() into unitGroup
    let total = unitGroup.Sum(item => item.Quantity)
    select new GroupSummary(unitGroup.Key.ToLowerInvariant(), unitGroup.Count(), total);
```

Це query syntax LINQ: `group ... by ... into` є читабельним для сценарію групування. Методичний (fluent) запис коротший для послідовних фільтрів і сортувань, тому він використаний у решті звітів. Перетворення ключа до одного регістру зберігає групування без урахування регістру.

## SampleData і приклад виконання

`SampleData` містить 18 товарів — це в межах 15–20 — та 4 групи одиниць: `шт`, `кг`, `м`, `л`. Для початкових даних `report-groups` дає такий результат:

```text
  шт     позицій:  10; сумарно:   4905
  кг     позицій:   3; сумарно:    768
  м      позицій:   3; сумарно:    690
  л      позицій:   2; сумарно:    155
```

`report-sum` друкує: `позицій 18, всього 6518, середнє 362,11, макс 4200`.

## Відкладене виконання

У `ReportService.DemonstrateDeferredExecution` запит `Where` створюється, але не виконується відразу. Перший `Count()` бачить початкове джерело, після додавання товару другий `Count()` бачить уже інше число. `ToList()` створює знімок; тому після `Clear()` джерела знімок не змінюється. CLI друкує всі три значення після кожного звіту.

## Додаткові завдання

| Пункт | Реалізація | Команда демонстрації |
| --- | --- | --- |
| 1. `Join` | `BatchesByGoods` поєднує `Goods.Id` і `Batch.GoodsId`, повертає DTO `BatchLine` | `report-batches` |
| 2. Extension-метод | `TopBy<T>(this IEnumerable<T>, Func<T,int>, int)` застосовано у `TopByQuantity` та `TopBatchesByQuantity` | `report-top-batches` |
| 3. Query syntax | `ByUnit` містить `from ... group ... into ...` | `report-groups` |
| 4. `Stopwatch` | `BenchmarkTopByQuantity` створює 100 000 товарів і порівнює конвеєр без та з `ToList()` у середині | `report-benchmark` |

`report-batches` показує чотири партії з `SampleData.Batches()` та товари, яким вони належать. `report-top-batches` показує три найбільші партії й одночасно доводить друге використання `TopBy<T>`.

Під час `report-benchmark` створення 100 000 записів відбувається **до** старту секундомірів. Потім вимірюються два рівнозначні звіти top-N: перший має один `ToList()` наприкінці, другий матеріалізує проміжний результат після `Where`. Числа в мілісекундах залежать від комп'ютера, але обидва варіанти повертають 5 рядків.

### Команди захисту: Definition of Done і додаткові пункти

```bash
# Обов'язкові чотири звіти
dotnet run --project src/Cli -f net10.0 --no-build -- report-top
dotnet run --project src/Cli -f net10.0 --no-build -- report-low
dotnet run --project src/Cli -f net10.0 --no-build -- report-groups
dotnet run --project src/Cli -f net10.0 --no-build -- report-sum

# Відкладене виконання видно наприкінці кожної з чотирьох команд вище.

# Додаткові пункти
dotnet run --project src/Cli -f net10.0 --no-build -- report-batches
dotnet run --project src/Cli -f net10.0 --no-build -- report-top-batches
dotnet run --project src/Cli -f net10.0 --no-build -- report-benchmark

# Автоматична перевірка всього LINQ-функціоналу
dotnet run --project tests/ReportChecks -f net10.0 --no-build
```

## Definition of Done

| Вимога | Реалізація |
| --- | --- |
| 4 LINQ-сценарії у Core | `LowStock`, `TopByQuantity`, `ByUnit`, `Totals` у `Core/Reports/ReportService.cs` |
| DTO та `IReadOnlyList` | `StockLine`, `GroupSummary`, `StockTotals`; спискові звіти повертають незмінні списки DTO |
| Стабільний top-N | `ThenBy` за `Sku` і `Id` |
| Порожнє джерело | Порожні списки та нульовий `StockTotals` без винятку |
| Щонайменше 4 CLI-команди | `report-top`, `report-low`, `report-groups`, `report-sum` |
| Відкладене виконання | Видимі числа до/після зміни та знімок після очищення |
| LINQ to Objects | Тільки `IEnumerable<Goods>`, без PLINQ і SQL |
| Дані й коміт | 18 товарів, 4 групи одиниць; коміт потрібно зробити з повідомленням `lab07` |
| Додаткові пункти | `Join`, `TopBy<T>`, query syntax і `Stopwatch` виконані та мають окремі CLI-команди |

## Перевірки

`tests/ReportChecks` перевіряє фільтрацію, DTO, детермінований порядок top-N, групування без урахування регістру, агрегати, порожнє джерело, відкладене виконання, Join, extension-метод, benchmark і дані SampleData.

```bash
dotnet run --project tests/ReportChecks -f net8.0 --no-build
dotnet run --project tests/ReportChecks -f net10.0 --no-build
dotnet run --project tests/DomainChecks -f net10.0 --no-build
dotnet run --project tests/ServiceChecks -f net10.0 --no-build
dotnet run --project tests/EventChecks -f net10.0 --no-build
```

## Файли лабораторної 7

| Статус | Файл | Призначення |
| --- | --- | --- |
| + | `src/Core/Reports/StockLine.cs` | DTO рядка звіту |
| + | `src/Core/Reports/GroupSummary.cs` | DTO групи |
| + | `src/Core/Reports/StockTotals.cs` | DTO агрегатів |
| + | `src/Core/Reports/DeferredExecutionResult.cs` | DTO демонстрації відкладеного виконання |
| + | `src/Core/Reports/BatchLine.cs` | DTO Join-звіту товарів і партій |
| + | `src/Core/Reports/LinqBenchmark.cs` | DTO вимірювання `Stopwatch` |
| + | `src/Core/Reports/EnumerableReportExtensions.cs` | Власний extension-метод `TopBy<T>` |
| + | `src/Core/Reports/ReportService.cs` | LINQ-звіти без Console |
| ± | `src/Core/SampleData.cs` | 18 товарів у 4 групах і партії для Join |
| ± | `src/Cli/Program.cs` | 4 обов'язкові та 3 додаткові команди |
| + | `tests/ReportChecks/*` | Перевірки лабораторної 7 |
| ± | `CrossApp.slnx` | Додано ReportChecks |
| ± | `README.md` | Запуск, докази й пояснення |

`+` — новий файл; `±` — змінений. Виконано як обов'язкову, так і всі чотири додаткові частини методички.
