Міністерство освіти і науки України
Львівський національний університет імені Івана Франка
Кафедра Системного проектування

Звіт
про виконання лабораторної роботи № 7
на тему:
«Звіти через LINQ: фільтри, проєкції, групування, топ-N, агрегати»

Виконав:
студент групи ФеІ-32
Струмінський Захарій

Перевірив:
Асистент
Берізка Ігор Андрійович

Львів – 2026

---

## 1 Мета та теоретичні відомості

**Мета роботи:** реалізувати аналітичне опрацювання колекції доменних об’єктів засобами LINQ to Objects: створити в шарі Core декілька запитів різних типів (фільтрація, сортування, групування, top-N, агрегати), забезпечити зрозуміле представлення їхніх результатів у Cli через DTO та дослідити особливості відкладеного виконання LINQ-запитів.

**Теоретичні положення:**
1. **LINQ to Objects** є набором методів розширення для інтерфейсу `IEnumerable<T>`. Умови відбору, селекції та групування передаються у формі типізованих делегатів, переважно `Func<T, TResult>`.
2. **Відкладене виконання (Deferred Execution):** оператори `Where`, `Select`, `OrderBy`, `GroupBy` описують конвеєр обробки, але не виконують ітерацію над джерелом у момент оголошення. Обчислення ініціюється під час фактичного обходу (через цикл `foreach`) або виклику термінальних методів матеріалізації (`ToList()`, `ToArray()`, `Count()`, `Sum()`, `Average()`, `Max()`, `First()`). Якщо базове джерело змінюється до початку ітерації, ці модифікації потрапляють у результат.
3. **Потокові та буферизувальні оператори:** оператори `Where` і `Select` є потоковими (streaming) — вони обробляють та передають елементи по одному без завантаження всієї послідовності. Оператори `OrderBy` і `GroupBy` є буферизувальними (buffering) — для сортування або формування груп вони змушені спочатку зчитати всю вхідну послідовність у пам'ять.
4. **Проєкція в DTO:** відокремлює зовнішній аналітичний контракт від внутрішньої доменної структури сутності `Goods`. Завдяки незмінним `record`-DTO контракт залишається стабільним, а внутрішня модель може змінюватися незалежно.
5. **Детермінованість top-N:** метод `OrderBy` у LINQ to Objects забезпечує стабільне сортування (зберігає взаємний порядок елементів з однаковими ключами). Для гарантування однакового результату між запусками незалежно від початкового порядку джерела застосовано вторинні унікальні ключі (`ThenBy` за `Sku` та `Id`).
6. **Агрегатні функції над порожніми послідовностями:** `Sum` повертає `0`, тоді як `Average` і `Max` для типів, що не допускають `null`, генерують виняток `InvalidOperationException`. Для запобігання цьому у коді реалізовано попередню перевірку розміру списку зі знімка (`items.Count == 0`).

---

## 2 Перелік аналітичних запитів та DTO предметної області

Відповідно до предметної області системи **Warehouse** (склад будівельних матеріалів) у класі `Core.Reports.ReportService` реалізовано 4 обов'язкові аналітичні звіти. Усі методи приймають `IEnumerable<Goods>`, не мають залежності від консолі (`System.Console`) і повертають строго типізовані контракти DTO.

### Таблиця аналітичних запитів

| Метод у `ReportService` | Питання, на яке відповідає | Використані LINQ-оператори | Тип результату |
| :--- | :--- | :--- | :--- |
| `LowStock(source, threshold)` | Яких товарів залишилося на складі менше вказаного порогу? | `Where`, `OrderBy`, `ThenBy`, `Select`, `ToList` | `IReadOnlyList<StockLine>` |
| `TopByQuantity(source, count)` | Які N позицій мають найбільший фізичний залишок? | `OrderBy`, `ThenBy`, `TopBy` (ext), `Select`, `ToList` | `IReadOnlyList<StockLine>` |
| `ByUnit(source)` | Скільки товарних позицій та одиниць продукції в кожній категорії виміру? | query syntax: `group ... by ... into`, `let`, `Sum`, `Count`, `Select`, `OrderByDescending`, `ThenBy`, `ToList` | `IReadOnlyList<GroupSummary>` |
| `Totals(source)` | Скільки всього позицій, сумарна кількість, середній та максимальний залишок? | `ToList`, `Count`, `Sum`, `Average`, `Max` | `StockTotals` |

### Структури DTO звітів
Для передачі аналітичних даних використано незмінні `record`:
- `StockLine(string Sku, string Name, int Quantity, string Unit)` — відображає рядок товарного звіту.
- `GroupSummary(string Key, int Count, int TotalQuantity)` — агреговані показники для кожної групи одиниць виміру.
- `StockTotals(int Items, int TotalQuantity, double AverageQuantity, int MaxQuantity)` — зведені складські показники.

Використання DTO гарантує ізоляцію: доменні інваріанти `Goods` захищені від випадкових модифікацій зовнішнім кодом, а клієнти (CLI зараз, API у лабораторній 10, Blazor у лабораторній 12) отримують лише необхідні поля без розкриття повної доменної сутності.

---

## 3 Реалізація LINQ-запитів: фільтрація, детермінований топ-N та агрегати

Усі запити зосереджені в статичному класі `Core.Reports.ReportService`. Методи написані як чисті функції над послідовністю.

### Фільтрація дефіцитних товарів (LowStock)
```csharp
public static IReadOnlyList<StockLine> LowStock(IEnumerable<Goods> source, int threshold)
{
    ArgumentNullException.ThrowIfNull(source);
    return source
        .Where(item => item.Quantity < threshold)
        .OrderBy(item => item.Quantity)
        .ThenBy(item => item.Sku, StringComparer.Ordinal)
        .ThenBy(item => item.Id, StringComparer.Ordinal)
        .Select(ToStockLine)
        .ToList()
        .AsReadOnly();
}
```
Оператор `Where` застосовується на початку ланцюжка, відсікаючи невідповідні елементи до сортування, що оптимізує використання пам'яті. Впорядкування виконується спочатку за зростанням залишку (`Quantity`), а при збігу значень — за `Sku` та внутрішнім ідентифікатором `Id` з використанням побайтового порівняння `StringComparer.Ordinal`.

### Детермінований топ-N (TopByQuantity)
```csharp
public static IReadOnlyList<StockLine> TopByQuantity(IEnumerable<Goods> source, int count)
{
    ArgumentNullException.ThrowIfNull(source);
    if (count < 0)
        throw new ArgumentOutOfRangeException(nameof(count), count, "Кількість позицій не може бути від'ємною.");

    return source
        .OrderBy(item => item.Sku, StringComparer.Ordinal)
        .ThenBy(item => item.Id, StringComparer.Ordinal)
        .TopBy(item => item.Quantity, count)
        .Select(ToStockLine)
        .ToList()
        .AsReadOnly();
}
```
Для формування детермінованого звіту елементи спочатку впорядковуються за унікальними ключами (`Sku`, `Id`). Далі власний метод-розширення `TopBy` виконує стабільне сортування за спаданням залишку `Quantity` та бере перші `count` елементів. Оскільки `OrderByDescending` зберігає вже встановлений порядок однакових елементів, багаторазові запуски гарантовано видають ідентичний порядок рядків.

### Агрегати зі захистом від порожнього джерела (Totals)
```csharp
public static StockTotals Totals(IEnumerable<Goods> source)
{
    ArgumentNullException.ThrowIfNull(source);
    List<Goods> items = source.ToList();
    return items.Count == 0
        ? new StockTotals(0, 0, 0d, 0)
        : new StockTotals(
            items.Count,
            items.Sum(item => item.Quantity),
            items.Average(item => item.Quantity),
            items.Max(item => item.Quantity));
}
```
Джерело матеріалізується в локальний список `items.ToList()`, щоб уникнути багаторазового проходу послідовності під час виклику чотирьох окремих агрегатних функцій. Перевірка `items.Count == 0` запобігає `InvalidOperationException` при виклику `Average` та `Max` над порожньою колекцією. Середнє повертається у форматі `double`, унеможливлюючи втрату дробової частини при цілочисельних залишках.

---

## 4 Групування даних (GroupBy) та проєкція

Для групування реалізовано метод `ByUnit`, який агрегує товари за одиницями виміру (`шт`, `кг`, `м`, `л`). Відповідно до методичних рекомендацій використано декларативний синтаксис запитів (query syntax):

```csharp
public static IReadOnlyList<GroupSummary> ByUnit(IEnumerable<Goods> source)
{
    ArgumentNullException.ThrowIfNull(source);
    var query =
        from item in source
        group item by item.Unit.ToUpperInvariant() into unitGroup
        let total = unitGroup.Sum(item => item.Quantity)
        select new GroupSummary(unitGroup.Key.ToLowerInvariant(), unitGroup.Count(), total);

    return query
        .OrderByDescending(summary => summary.TotalQuantity)
        .ThenBy(summary => summary.Key, StringComparer.OrdinalIgnoreCase)
        .ThenBy(summary => summary.Key, StringComparer.Ordinal)
        .ToList()
        .AsReadOnly();
}
```

**Особливості реалізації:**
- Ключем групування виступає `item.Unit.ToUpperInvariant()`, що об'єднує значення в єдину групу без чутливості до регістру введення (наприклад, "шт" і "ШТ").
- У конструкції `into unitGroup`: змінна `unitGroup.Key` зберігає спільний ключ групи, `unitGroup.Count()` рахує кількість товарних найменувань, а `unitGroup.Sum(...)` обчислює сумарний залишок одиниць.
- Оператор `let` обчислює суму один раз і передає її в проєкцію `select new GroupSummary(...)`.
- Результуючий список сортується за спаданням сумарної кількості `TotalQuantity`, а при однакових сумах — за назвою ключа в алфавітному порядку.

---

## 5 Дослідження відкладеного виконання (Deferred Execution)

Для наочної демонстрації механізму відкладеного виконання в `ReportService` розроблено метод `DemonstrateDeferredExecution`:

```csharp
public static DeferredExecutionResult DemonstrateDeferredExecution(IEnumerable<Goods> source, int threshold)
{
    ArgumentNullException.ThrowIfNull(source);
    var items = source.ToList();
    IEnumerable<Goods> query = items.Where(item => item.Quantity < threshold);
    int beforeChange = query.Count();

    items.Add(Goods.Create("DEFERRED-007", "SKU-DEFERRED", "Товар для LINQ-демо", "шт", 3));
    int afterChange = query.Count();

    List<Goods> snapshot = query.ToList();
    items.Clear();
    return new DeferredExecutionResult(beforeChange, afterChange, snapshot.Count);
}
```

**Аналіз результатів та чисел з демо:**
1. Оголошення `IEnumerable<Goods> query = items.Where(...)` лише створює ітератор і не фільтрує елементи негайно.
2. Перший виклик термінального методу `query.Count()` проходить колекцію з 18 товарів і знаходить рівно **1** дефіцитний товар (`SKU-018` Захисні окуляри із залишком 0).
3. Після мутації списку `items.Add(...)` новим товаром із залишком 3 (< 10) повторний виклик `query.Count()` знову ітерує змінену колекцію й повертає **2**. Це доводить, що запит обчислюється в момент виклику над актуальним станом даних.
4. Виклик `snapshot = query.ToList()` матеріалізує запит у новий окремий об'єкт `List<Goods>`.
5. Після повного очищення вихідної колекції `items.Clear()` властивість `snapshot.Count` продовжує містити **2**, оскільки знімок зафіксовано в пам'яті і він не зазнає впливу подальших змін вихідного джерела.

---

## 6 Демонстрація роботи програми

Роботу CLI-застосунку протестовано у середовищі Windows за допомогою Git Bash. Початковий набір даних `SampleData` містить 18 товарів у 4 групах одиниць виміру (`шт`, `кг`, `м`, `л`).

### 1. Підкоманда report-top (топ-5 за залишком)
Команда: `dotnet run --project src/Cli -f net10.0 --no-build -- report-top`

![Рисунок 1 — Робота підкоманди report-top та демонстрація відкладеного виконання LINQ](file:///C:/Users/HP/.gemini/antigravity/brain/e6e634db-088f-4da8-bb1d-e0d72625a48f/.user_uploaded/media_1793649105893_290b8bc0.png)
*Рисунок 1 — Робота підкоманди report-top та демонстрація відкладеного виконання LINQ*

Вивід демонструє детермінований список 5 товарів з найбільшими залишками: Цегла (4200 шт), Клей для плитки (500 кг), Кабель UTP cat6 (350 м), Шпаклівка (250 кг), Утеплювач (200 шт).

### 2. Підкоманда report-low (дефіцитні позиції)
Команда: `dotnet run --project src/Cli -f net10.0 --no-build -- report-low`

![Рисунок 2 — Робота підкоманди report-low](file:///C:/Users/HP/.gemini/antigravity/brain/e6e634db-088f-4da8-bb1d-e0d72625a48f/.user_uploaded/media_1793649134560_2c0f7e9c.png)
*Рисунок 2 — Робота підкоманди report-low*

Звіт відображає позиції із залишком менше 10: захисні окуляри `SKU-018` із нульовим залишком (0 шт).

### 3. Підкоманда report-groups (групування за одиницями виміру)
Команда: `dotnet run --project src/Cli -f net10.0 --no-build -- report-groups`

![Рисунок 3 — Робота підкоманди report-groups](file:///C:/Users/HP/.gemini/antigravity/brain/e6e634db-088f-4da8-bb1d-e0d72625a48f/.user_uploaded/media_1793649140054_8f4253f9.png)
*Рисунок 3 — Робота підкоманди report-groups*

Згруповані результати коректно відсортовані за спаданням сумарного залишку: штуки (10 позицій, 4905 шт), кілограми (3 позиції, 768 кг), метри (3 позиції, 690 м), літри (2 позиції, 155 л).

### 4. Підкоманда report-sum (агрегати складу)
Команда: `dotnet run --project src/Cli -f net10.0 --no-build -- report-sum`

```text
HP@DESKTOP-2F66P5I MINGW64 /c/dev/CrossApp (main)
$ dotnet run --project src/Cli -f net10.0 --no-build -- report-sum
Лабораторна робота 7 — LINQ to Objects для Warehouse
Сховище: InMemoryCatalogStore; товарів: 18

=== report-sum: підсумки складу ===
  позицій 18, всього 6518, середнє 362,11, макс 4200

=== Відкладене виконання LINQ ===
До зміни джерела: 1
Після додавання товару: 2
Знімок після очищення джерела: 2
```

Звіт агрегатних функцій підтверджує наявність 18 позицій із сумарним залишком 6518 одиниць, точним середнім значенням 362,11 та максимумом 4200.

---

## 7 Додаткові завдання

У проєкті виконано всі 4 додаткові завдання, передбачені методичними вказівками:

### 1. Оператор Join для об'єднання двох колекцій
Метод `ReportService.BatchesByGoods` поєднує колекцію `IEnumerable<Goods>` та колекцію партій `IEnumerable<Batch>` за ключем `Goods.Id == Batch.GoodsId`:

```csharp
public static IReadOnlyList<BatchLine> BatchesByGoods(IEnumerable<Goods> goods, IEnumerable<Batch> batches)
{
    ArgumentNullException.ThrowIfNull(goods);
    ArgumentNullException.ThrowIfNull(batches);
    return goods
        .Join(
            batches,
            goods => goods.Id,
            batch => batch.GoodsId,
            (goods, batch) => new BatchLine(
                batch.Id,
                goods.Sku,
                goods.Name,
                batch.Quantity,
                batch.ReceivedDate,
                batch.Status),
            StringComparer.OrdinalIgnoreCase)
        .OrderBy(line => line.ReceivedDate)
        .ThenBy(line => line.BatchId, StringComparer.Ordinal)
        .ToList()
        .AsReadOnly();
}
```

Вивід команди `dotnet run --project src/Cli -f net10.0 --no-build -- report-batches`:
```text
=== Додаткове 1: Join товарів і партій ===
  B-001  SKU-003    Цегла                     1000 2026-10-01 Draft
  B-002  SKU-006    Кабель UTP cat6            200 2026-10-05 Draft
  B-003  SKU-012    Клей для плитки            150 2026-10-10 Draft
  B-004  SKU-018    Захисні окуляри              5 2026-10-15 Draft
```

### 2. Власний метод-розширення TopBy<T>
У файлі `Core/Reports/EnumerableReportExtensions.cs` створено узагальнений extension-метод:
```csharp
public static IReadOnlyList<T> TopBy<T>(this IEnumerable<T> source, Func<T, int> keySelector, int count)
{
    ArgumentNullException.ThrowIfNull(source);
    ArgumentNullException.ThrowIfNull(keySelector);
    if (count < 0)
        throw new ArgumentOutOfRangeException(nameof(count), count, "Кількість позицій не може бути від'ємною.");

    return source
        .OrderByDescending(keySelector)
        .Take(count)
        .ToList()
        .AsReadOnly();
}
```
Метод застосовано двічі: у звіті `TopByQuantity` для товарів та у звіті `TopBatchesByQuantity` для партій. Вивід команди `report-top-batches`:
```text
=== Додаткове 2: TopBy<T> для партій ===
  B-001  SKU-003    Цегла                     1000 2026-10-01 Draft
  B-002  SKU-006    Кабель UTP cat6            200 2026-10-05 Draft
  B-003  SKU-012    Клей для плитки            150 2026-10-10 Draft
```

### 3. Query Syntax (синтаксис запитів)
Реалізовано в методі `ByUnit` за допомогою ключових слів `from ... in ... group ... by ... into ... let ... select ...`. Це забезпечує вищу декларативну читабельність порівняно з ланцюжком викликів `GroupBy` та вкладених лямбд.

### 4. Вимірювання продуктивності за допомогою Stopwatch
Метод `BenchmarkTopByQuantity` генерує 100 000 записів до старту таймерів і порівнює швидкість виконання конвеєра top-N без проміжного `ToList()` та з проміжним `ToList()` після фільтрації `Where`:
```csharp
var withoutMiddleToList = Stopwatch.StartNew();
List<StockLine> direct = source
    .Where(item => item.Quantity >= 0)
    .OrderByDescending(item => item.Quantity)
    .ThenBy(item => item.Sku, StringComparer.Ordinal)
    .Take(count)
    .Select(ToStockLine)
    .ToList();
withoutMiddleToList.Stop();

var withMiddleToList = Stopwatch.StartNew();
List<Goods> filtered = source.Where(item => item.Quantity >= 0).ToList();
List<StockLine> materialized = filtered
    .OrderByDescending(item => item.Quantity)
    .ThenBy(item => item.Sku, StringComparer.Ordinal)
    .Take(count)
    .Select(ToStockLine)
    .ToList();
withMiddleToList.Stop();
```

Вивід команди `dotnet run --project src/Cli -f net10.0 --no-build -- report-benchmark`:
```text
=== Додаткове 4: Stopwatch на 100 000 записів ===
  Записів: 100000; top-N: 5 / 5
  Без ToList у середині: 29 мс
  З ToList у середині:   9 мс
  Числа залежать від комп'ютера; обидва варіанти повертають однакову кількість рядків.
```

---

## 8 Перевірки та висновок

Для автоматичної валідації аналітичних методів виконано запуск тестового набору `tests/ReportChecks`:
Команда: `dotnet run --project tests/ReportChecks -f net10.0 --no-build`

```text
HP@DESKTOP-2F66P5I MINGW64 /c/dev/CrossApp (main)
$ dotnet run --project tests/ReportChecks -f net10.0 --no-build
PASS LowStock filters, orders deterministically and maps to DTO
PASS TopByQuantity uses secondary and tertiary keys and accepts zero
PASS ByUnit groups case-insensitively, aggregates and orders summaries
PASS Totals returns correct values and handles an empty source
PASS Deferred execution sees a changed source; ToList snapshot survives Clear
PASS Join report maps related Goods and Batch entities to DTO and ignores unmatched batches
PASS TopBy extension is stable after a deterministic pre-sort and is used for joined batches
PASS Benchmark generates 100000 records and both equivalent pipelines return top-N
PASS Report methods reject null sources and seed meets the laboratory data requirements
Report checks: 9 passed, 0 failed
```

Усі 9 тестів успішно пройдено. Загальний набір перевірок рішення CrossApp також підтверджує повну стабільність усіх попередніх модулів:
- `DomainChecks`: 56 passed, 0 failed.
- `ServiceChecks`: 45 passed, 0 failed.
- `EventChecks`: 34 passed, 0 failed.
- `ReportChecks`: 9 passed, 0 failed.

**Висновок:** У ході виконання лабораторної роботи № 7 засобами LINQ to Objects реалізовано аналітичну підсистему складу `Warehouse`. Створено методи фільтрації (`LowStock`), детермінованого сортування (`TopByQuantity`), нечутливого до регістру групування (`ByUnit`) та безпечного обчислення агрегатів (`Totals`). Досліджено механізм відкладеного виконання та ефект фіксації результату через `ToList()`. Реалізовано всі 4 додаткові завдання: міжколекційний `Join`, власний метод-розширення `TopBy<T>`, query syntax та порівняльний бенчмарк за допомогою `Stopwatch`.
