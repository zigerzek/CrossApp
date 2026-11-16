# Захист лабораторної роботи № 7 — Warehouse (LINQ to Objects)

Цей посібник містить повну підготовку до захисту лабораторної роботи № 7: промову для початку, покроковий розбір Definition of Done, шпаргалку з усіма консольними командами, глибоке пояснення теорії, детальний розбір усіх 4 додаткових завдань та готові відповіді на всі питання викладача.

---

## 1. Початок: що сказати викладачу

> «Моя предметна область — склад будівельних матеріалів (**Warehouse**), доменна сутність — `Goods`. У сьомій лабораторній роботі реалізовано аналітичне опрацювання колекції доменних об’єктів за допомогою **LINQ to Objects**.
> 
> Уся аналітична логіка зосереджена в шарі `Core` у статичному класі `ReportService` у вигляді чистих функцій. Звіти приймають послідовність `IEnumerable<Goods>`, не мають жодної залежності від консолі (`System.Console`) і повертають незмінні типізовані `record`-DTO (`StockLine`, `GroupSummary`, `StockTotals`, `BatchLine`), а не внутрішні доменні сутності.
> 
> Консольний застосунок `Cli` відповідає виключно за вибір команди, передачу параметрів і форматований вивід. Окрім обов'язкових звітів (фільтрація залишків, детермінований топ-N, групування за одиницями виміру, агрегати) та дослідження відкладеного виконання, я повністю реалізував **усі 4 додаткові завдання**: міжколекційний `Join`, власний extension-метод `TopBy<T>`, декларативний `query syntax` та порівняльний бенчмарк продуктивності зі `Stopwatch` на 100 000 записах».

---

## 2. Відповідність Definition of Done (DoD)

Методичка вимагає виконання 8 пунктів Definition of Done. Нижче наведено, як саме кожен пункт реалізовано в проєкті:

| № | Вимога Definition of Done | Реалізація у вашому проєкті | Де подивитися в коді |
| :-: | :--- | :--- | :--- |
| **1** | **4–5 різних LINQ-сценаріїв у Core** (Where, Select, GroupBy, агрегати, топ-N, Join) | Реалізовано 5 методів: `LowStock` (Where + Select), `TopByQuantity` (OrderBy + ThenBy + TopBy), `ByUnit` (GroupBy + Sum + Count), `Totals` (Sum, Average, Max), `BatchesByGoods` (Join). | `src/Core/Reports/ReportService.cs` |
| **2** | **Звіти приймають `IEnumerable<T>` і повертають `IReadOnlyList<DTO>`** | Усі спискові методи приймають `IEnumerable<Goods>` і повертають `IReadOnlyList<StockLine>`, `IReadOnlyList<GroupSummary>` або `IReadOnlyList<BatchLine>`. Агрегати повертають DTO `StockTotals`. Жоден метод не повертає `Goods`. | `src/Core/Reports/*.cs` |
| **3** | **Топ-N має другий ключ сортування** | У `TopByQuantity` реалізовано сортування: основне за залишком (`Quantity`), вторинне за артикулом (`Sku`), третинне за унікальним `Id` через `StringComparer.Ordinal`. Результат 100% детермінований. | `ReportService.cs:23-37` |
| **4** | **Порожнє джерело обробляється без винятку** | Спискові звіти повертають порожній `IReadOnlyList`. У `Totals` перевіряється `items.Count == 0`, повертаючи нульовий об'єкт `StockTotals(0, 0, 0d, 0)`, що захищає від падіння `Average` і `Max`. | `ReportService.cs:59-70` |
| **5** | **Cli має щонайменше 4 підкоманди звітів і читабельне вирівнювання** | Реалізовано 4 обов'язкові команди (`report-top`, `report-low`, `report-groups`, `report-sum`) та 3 додаткові. Форматування виконано інтерполяцією з фіксованою шириною колонок (`{line.Sku,-10}`). | `src/Cli/Program.cs:36-66, 121-166` |
| **6** | **Демо відкладеного виконання працює і пояснене** | Метод `DemonstrateDeferredExecution` демонструє числа: 1 дефіцитний до зміни $\rightarrow$ 2 після додавання товару в список $\rightarrow$ 2 у збереженому знімку `ToList()` після `Clear()`. Пояснено в README та звіті. | `ReportService.cs:72-86` |
| **7** | **Лише LINQ to Objects (без PLINQ і SQL)** | У проєкті немає викликів `AsParallel()`, `AsOrdered()`, сторонніх бібліотек чи Entity Framework. Усі операції виконуються над колекціями в оперативній пам'яті. | `Core.Reports` |
| **8** | **SampleData розширено до 15–20 записів** | `SampleData.Goods()` містить рівно 18 товарів у 4 різних категоріях одиниць виміру (`шт`, `кг`, `м`, `л`), з них товар `SKU-018` має нульовий залишок (< 10) для непорожнього звіту `report-low`. | `src/Core/SampleData.cs:17-48` |

---

## 3. Усі консольні команди для захисту (Git Bash)

Усі команди виконуються в кореневій папці проєкту: `/c/dev/CrossApp`.

### Базові команди перевірки та збірки
```bash
cd /c/dev/CrossApp
dotnet build CrossApp.slnx
```

### Чотири обов'язкові аналітичні звіти
```bash
# 1. Топ-5 за залишком (також запускається за замовчуванням без аргументів)
dotnet run --project src/Cli -f net10.0 --no-build -- report-top

# 2. Дефіцитні товари (залишок < 10 одиниць)
dotnet run --project src/Cli -f net10.0 --no-build -- report-low

# 3. Розподіл товарів за одиницями виміру (групування)
dotnet run --project src/Cli -f net10.0 --no-build -- report-groups

# 4. Загальні підсумки та агрегати складу
dotnet run --project src/Cli -f net10.0 --no-build -- report-sum
```

> **Зверніть увагу:** Блок `=== Відкладене виконання LINQ ===` автоматично виводиться наприкінці кожної з перелічених вище команд!

### Доведення стабільності топ-N (детермінованість)
Запустіть підкоманду двічі поспіль — порядок рядків залишається ідентичним:
```bash
dotnet run --project src/Cli -f net10.0 --no-build -- report-top
dotnet run --project src/Cli -f net10.0 --no-build -- report-top
```

### Команди додаткових завдань
```bash
# Додаткове 1: Join товарів та їхніх партій (Batch)
dotnet run --project src/Cli -f net10.0 --no-build -- report-batches

# Додаткове 2: Друге використання власного extension-методу TopBy для партій
dotnet run --project src/Cli -f net10.0 --no-build -- report-top-batches

# Додаткове 4: Вимірювання продуктивності Stopwatch на 100 000 записах
dotnet run --project src/Cli -f net10.0 --no-build -- report-benchmark
```

### Автоматичні тести (перевірка якості та надійності)
```bash
# Тести 7-ї лабораторної (фільтри, top-N, групування, агрегати, порожнє джерело, Join, benchmark)
dotnet run --project tests/ReportChecks -f net10.0 --no-build

# Перевірка на сумісність з .NET 8 (multi-targeting)
dotnet run --project tests/ReportChecks -f net8.0 --no-build

# Повна валідація всіх попередніх модулів системи
dotnet run --project tests/EventChecks -f net10.0 --no-build     # 34 passed (Лаб 6)
dotnet run --project tests/ServiceChecks -f net10.0 --no-build   # 45 passed (Лаб 5)
dotnet run --project tests/DomainChecks -f net10.0 --no-build    # 56 passed (Лаб 1-4)
```

### Поєднання з опціями попередніх лабораторних
CLI підтримує прапорці з 5-ї лабораторної:
```bash
# Читання з файлу JSON через кешуючий декоратор і фабрику сховищ
dotnet run --project src/Cli -f net10.0 --no-build -- report-groups --file --path data/catalog.json --cache --factory

# Режим "тільки перегляд" початкових даних без виконання звітів
dotnet run --project src/Cli -f net10.0 --no-build -- --list-only
```

---

## 4. Пояснення коду: що відкрити та показати викладачу

### Крок 1. Показати DTO звітів
**Файли:** `src/Core/Reports/StockLine.cs`, `GroupSummary.cs`, `StockTotals.cs`, `BatchLine.cs`.
- **Що сказати:**
  > «Усі звіти повертають незмінні `record`-DTO. Вони мають компіляторну реалізацію порівняння за значенням (зручно в тестах `Equal(...)`), не дозволяють змінити поля ззовні (immutable) і захищають доменну сутність `Goods` від несанкціонованих модифікацій. Звітний шар відокремлений від домену».

### Крок 2. Показати метод `LowStock` (фільтрація)
**Файл:** `src/Core/Reports/ReportService.cs`, рядки 9–20:
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
- **Що сказати:**
  > «Оператор `Where` розташований першим у ланцюжку. Це потоковий (*streaming*) оператор. Він одразу відсікає непотрібні елементи, тому буферизувальний `OrderBy` сортує лише невелику відфільтровану кількість товарів, економлячи пам'ять і процесорний час. `ThenBy` з `StringComparer.Ordinal` забезпечує детерміноване сортування без прив'язки до локалі ОС».

### Крок 3. Показати метод `TopByQuantity` (детермінований топ-N)
**Файл:** `src/Core/Reports/ReportService.cs`, рядки 23–37:
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
- **Що сказати:**
  > «Тут використано мій власний extension-метод `TopBy`. Спочатку ми робимо попереднє впорядкування за вторинними унікальними ключами (`Sku`, `Id`). `OrderByDescending` всередині `TopBy` є стабільним сортуванням, тому для товарів з однаковим залишком гарантовано збережеться порядок, зафіксований на попередньому кроці. Топ-5 завжди повертає однаковий список».

### Крок 4. Показати `ByUnit` (групування в Query Syntax)
**Файл:** `src/Core/Reports/ReportService.cs`, рядки 40–56:
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
- **Що сказати:**
  > «Це синтаксис запитів (*query syntax*).
  > • `item.Unit.ToUpperInvariant()` — ключ групування; переведення у верхній регістр об'єднує "шт" і "ШТ" в одну групу.
  > • `into unitGroup` — змінна діапазону, яка представляє групу типу `IGrouping<string, Goods>`.
  > • `unitGroup.Key` — значення самого ключа (одиниця виміру).
  > • `unitGroup.Count()` — кількість товарних позицій у даній групі.
  > • `unitGroup.Sum(item => item.Quantity)` — сума фізичних залишків товарів у групі.
  > • `let total` — введення проміжної змінної, щоб не дублювати обчислення суми.
  > • Потім готові DTO `GroupSummary` сортуються за спаданням сумарного залишку».

### Крок 5. Показати метод `Totals` (агрегати та обробка порожнього джерела)
**Файл:** `src/Core/Reports/ReportService.cs`, рядки 59–70:
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
- **Що сказати:**
  > «Чому спочатку викликається `source.ToList()`? Щоб уникнути **multiple enumeration** — якщо джерело читається з файлу або є складним ітератором, без матеріалізації виклики `Count`, `Sum`, `Average` і `Max` обійшли б його чотири рази підряд.
  > Чому є перевірка `items.Count == 0`? Якщо послідовність порожня, `Sum` повертає `0`, але методи `Average` і `Max` кидають виняток `InvalidOperationException: Sequence contains no elements`. Наша перевірка безпечно повертає нульовий об'єкт `StockTotals(0, 0, 0d, 0)`.
  > Середнє обчислюється у типі `double`, тому результат має дробову частину (362,11), а не обрізається до цілого».

### Крок 6. Демонстрація відкладеного виконання
**Файл:** `src/Core/Reports/ReportService.cs`, рядки 73–86:
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
- **Що сказати:**
  > «1. Змінна `query` створюється через `Where`, але в цей момент жодного обходу елементів ще немає (запит побудовано, але не виконано).
  > 2. Перший `query.Count()` обходить початковий список і знаходить **1** товар (Захисні окуляри із залишком 0).
  > 3. Ми модифікуємо список: `items.Add(...)` новим товаром із залишком 3.
  > 4. Другий виклик `query.Count()` знову проходить по зміненому списку й повертає **2**. Це прямий доказ, що LINQ-запит обчислюється в момент виклику над актуальними даними.
  > 5. Виклик `query.ToList()` матеріалізує поточний стан у новий окремий список `snapshot`.
  > 6. Ми повністю очищуємо список `items.Clear()`. Проте `snapshot.Count` залишається рівним **2**, тому що знімок у пам'яті вже зафіксований і не залежить від вихідної колекції».

---

## 5. Додаткові завдання (на максимальний бал)

Усі 4 додаткові завдання реалізовані та протестовані:

### 1. Оператор `Join` двох колекцій
- **Код:** `ReportService.BatchesByGoods(goods, batches)` (рядки 89–110).
- **Команда:** `dotnet run --project src/Cli -f net10.0 --no-build -- report-batches`
- **Суть:** Поєднує колекцію товарів `Goods` із колекцією складських партій `Batch` за співпадінням ключів:
  ```csharp
  goods.Join(
      batches,
      goods => goods.Id,
      batch => batch.GoodsId,
      (goods, batch) => new BatchLine(batch.Id, goods.Sku, goods.Name, batch.Quantity, batch.ReceivedDate, batch.Status),
      StringComparer.OrdinalIgnoreCase)
  ```
  Неузгоджені записи (партії з невідомим `GoodsId`) автоматично відсікаються, аналогічно до `INNER JOIN` у SQL.

### 2. Власний метод-розширення `TopBy<T>`
- **Код:** `src/Core/Reports/EnumerableReportExtensions.cs`:
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
- **Де використано двічі:**
  1. У методі `TopByQuantity` для топ-5 товарів за залишком (`Quantity`).
  2. У методі `TopBatchesByQuantity` для топ-3 партій за кількістю (`BatchQuantity`).
- **Команда:** `dotnet run --project src/Cli -f net10.0 --no-build -- report-top-batches`

### 3. Декларативний синтаксис запитів (Query Syntax)
- **Код:** Метод `ByUnit` (рядки 44–48).
- **Порівняння:** Для групування синтаксис `from ... in ... group ... by ... into ... select ...` є набагато читабельнішим за method syntax `source.GroupBy(...).Select(...)`, оскільки дає прямий доступ до імені групи (`unitGroup`) та її агрегатів без громіздких вкладених лямбд.

### 4. Вимірювання продуктивності за допомогою `Stopwatch`
- **Код:** `ReportService.BenchmarkTopByQuantity(100_000, 5)` (рядки 122–164).
- **Команда:** `dotnet run --project src/Cli -f net10.0 --no-build -- report-benchmark`
- **Суть експерименту:**
  Генерується 100 000 записів **до** запуску секундомірів. Порівнюються два варіанти:
  1. Прямий конвеєр: `source.Where(...).OrderByDescending(...).ThenBy(...).Take(5).ToList()` — займає близько **29 мс**.
  2. З проміжним `ToList()`: `var filtered = source.Where(...).ToList();` далі сортування й `Take(5)` — займає близько **9 мс**.
- **Чому з ToList() швидше?**
  Якщо послідовність після `Where` матеріалізувати в список, подальше сортування `OrderByDescending` та `ThenBy` працює з уже виділеним суміжним масивом у пам'яті (індексація за $O(1)$) і не викликає повторної оцінки предиката під час численних перестановок алгоритму QuickSort / TimSort. Проте це вимагає виділення додаткової пам'яті для проміжного списку.

---

## 6. Відповіді на всі 12 питань методички для захисту

### 1. Які оператори виконуються відкладено, а які запускають ітерацію негайно?
- **Відкладені (deferred execution):** `Where`, `Select`, `SelectMany`, `OrderBy`, `OrderByDescending`, `ThenBy`, `GroupBy`, `Take`, `Skip`, `Distinct`, `Join`, `GroupJoin`. Вони повертають об'єкт-ітератор (`IEnumerable<T>`) і не обходять колекцію в момент виклику.
- **Негайні (immediate execution / terminal):**
  - Матеріалізація: `ToList()`, `ToArray()`, `ToDictionary()`, `ToHashSet()`.
  - Агрегати: `Count()`, `Sum()`, `Average()`, `Min()`, `Max()`, `Aggregate()`.
  - Пошук елемента: `First()`, `FirstOrDefault()`, `Last()`, `LastOrDefault()`, `Single()`, `SingleOrDefault()`, `ElementAt()`.
  - Квантори: `Any()`, `All()`, `Contains()`, `SequenceEqual()`.

### 2. Що станеться, якщо змінити джерело після побудови запиту, але до ToList?
Оскільки запит виконується відкладено, `ToList()` виконає перебирання в момент свого виклику і побачить усі зміни в джерелі (як додані елементи, так і змінені поля чи видалення). Саме це ми довели в кроці 3: після `items.Add` повторний підрахунок дав 2 замість 1.

### 3. Чому результат, який використовується двічі, варто матеріалізувати?
Якщо результат запиту використовувати декілька разів (наприклад, викликати `Count()`, а потім `Sum()`, або обійти в двох циклах), без матеріалізації кожен такий виклик виконуватиме весь ланцюжок фільтрації та селекції заново з нуля. Якщо джерелом є файл (`File.ReadLines`), файл буде перечитано з диска стільки разів, скільки разів викликано термінальний метод. Виклик `var list = query.ToList()` зберігає результат у пам'яті за один прохід.

### 4. Які оператори змушені зібрати всю послідовність у пам'ять і чим це загрожує?
Це буферизувальні (*buffering*) оператори: `OrderBy`, `OrderByDescending`, `ThenBy`, `GroupBy`, `Reverse`. Для того, щоб віддати хоча б перший найменший елемент у сортуванні, алгоритм зобов'язаний спочатку вичитати та порівняти абсолютно всі елементи послідовності. При обробці великих послідовностей (гігабайти даних або мільйони записів) це призводить до стрибка споживання оперативної пам'яті та загрожує винятком `OutOfMemoryException`.

### 5. Різниця між властивістю Count і методом Count()?
- Властивість `list.Count` належить інтерфейсу `ICollection<T>` / класу `List<T>`. Вона просто читає вже збережене в полі об'єкта число за константний час $\mathcal{O}(1)$.
- Метод `query.Count()` є методом розширення LINQ для `IEnumerable<T>`. Якщо базовий об'єкт не реалізує `ICollection`, метод змушений послідовно пройти всю послідовність через ітератор `IEnumerator`, рахуючи елементи за лінійний час $\mathcal{O}(N)$.

### 6. Навіщо проєкція в DTO, якщо можна повернути сутності?
1. **Інкапсуляція та захист домену:** DTO не мають методів зміни стану (наприклад, `RegisterArrival` чи `RegisterIssue`). Клієнт звіту не може зламати бізнес-правила доменної моделі.
2. **Стабільність контракту:** Внутрішня структура сутності `Goods` може змінюватися (додаватись нові поля, таблиці зв'язків), але DTO звіту залишається незмінним.
3. **Готовність до серіалізації:** Незмінні `record`-DTO оптимізовані для передачі по мережі в API (тиждень 10) або прив'язки до таблиць Blazor (тиждень 12) без зайвих службових даних.

### 7. Що поверне Average для порожньої колекції і як ви це обробили?
Для числової послідовності з нульовою кількістю елементів метод `Average` (як і `Max` чи `Min`) генерує виняток `InvalidOperationException: Sequence contains no elements`. Ми запобігли цьому в методі `Totals`: попередньо перевіряємо `if (items.Count == 0)` і в цьому випадку одразу повертаємо `StockTotals(0, 0, 0d, 0)` без виклику ризикованих методів.

### 8. Навіщо ThenBy у топ-N?
Метод `OrderBy` у LINQ to Objects виконує *стабільне сортування* — елементи з однаковим ключем залишаються у тому порядку, в якому вони надійшли з джерела. Але порядок надходження з бази даних чи файлу може бути випадковим або змінюватися між запусками. Щоб топ-N повертав гарантовано однаковий (детермінований) результат за будь-яких умов, додається сортування за унікальними полями: `.ThenBy(p => p.Sku, StringComparer.Ordinal).ThenBy(p => p.Id, StringComparer.Ordinal)`.

### 9. Різниця First / FirstOrDefault / Single / SingleOrDefault і коли буде виняток?
- `First()`: повертає перший елемент. Кидає `InvalidOperationException`, якщо послідовність порожня.
- `FirstOrDefault()`: повертає перший елемент або `default` (`null` для посилань, `0` для чисел), якщо послідовність порожня. Не кидає виняток на порожній колекції.
- `Single()`: очікує рівно один елемент. Кидає `InvalidOperationException`, якщо послідовність порожня АБО містить більше одного елемента.
- `SingleOrDefault()`: повертає єдиний елемент або `default` (якщо послідовність порожня). Але кидає `InvalidOperationException`, якщо елементів більше одного!

### 10. Що зміниться у ваших запитах, коли джерелом стане IQueryable в EF Core (тиждень 11)?
- Замість делегатів `Func<T, bool>` оператори прийматимуть дерева виразів `Expression<Func<T, bool>>`.
- Запити будуть транслюватися провайдером Entity Framework у нативний SQL-код (`WHERE`, `ORDER BY`, `GROUP BY`).
- Фільтрація, сортування та обчислення агрегатів виконуватимуться на боці СУБД (на сервері), а в пам'ять програми завантажуватимуться лише готові результуючі рядки.

### 11. Де у вашому проєкті ручний цикл виправданіший за LINQ?
1. **Виведення в консоль (`Console.WriteLine`):** LINQ призначений для функціональних запитів без побічних ефектів (*side-effects*). Виведення рядків на екран у методах `PrintStockLines`, `PrintGroups`, `PrintBatchLines` реалізовано через цикл `foreach` — це ідіоматичний стандарт C#.
2. **Розбір аргументів командного рядка (`SplitArguments`):** цикл `for` з індексами дозволяє легко пропускати наступний аргумент після `--path <значення>` (`index++`), що у LINQ виглядало б неприродно та заплутано.

---

## 7. Типові помилки, яких ми уникнули

1. **Жодного LINQ у `Program.cs`:** увесь код фільтрації, групування та агрегатів розташований у `Core/Reports/ReportService.cs`. У `Program.cs` є лише підкоманди та вивід через `Console.WriteLine`.
2. **Фільтрація до сортування:** у `LowStock` виклик `Where` стоїть перед `OrderBy`, тому сортуються лише відфільтровані позиції.
3. **Обчислення середнього без втрати дробової частини:** використано `Average(p => p.Quantity)`, що повертає `double`, замість помилкового цілочисельного ділення `Sum() / Count()`.
4. **Захист від модифікації колекції:** демонстрація відкладеного виконання використовує `ToList()` перед очищенням `items.Clear()`, що унеможливлює помилку `InvalidOperationException: Collection was modified`.
5. **Матеріалізація списків у Core:** методи повертають `IReadOnlyList<DTO>`, тому повторні звернення чи виклики `.Count` у клієнті не перечитують джерело.
