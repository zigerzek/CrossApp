# CrossApp — лабораторна робота 6

**Доменні події, делегати Action/Func, лямбди.** Тема: склад (Warehouse), сутність Goods. Реалізовано основне завдання та обидва додаткові завдання методички. Сховища, інваріанти, DTO, фабрика та кеш із попередніх робіт збережені; CLI тепер демонструє лабораторну 6.

## Запуск у Git Bash

```bash
cd /c/dev/CrossApp
dotnet build CrossApp.slnx
dotnet run --project src/Cli -f net10.0 --no-build
echo $?
tail -n 5 src/Cli/bin/Debug/net10.0/logs/app.log
tail -n 4 src/Cli/bin/Debug/net10.0/logs/sink.log
dotnet run --project tests/EventChecks -f net10.0 --no-build
```

Звичайний запуск показує основне завдання й **обидва додаткові**. Окремих прапорців для додаткових завдань немає. Працює також `-f net8.0`; тоді журнали будуть у відповідній папці `net8.0`.

## Definition of Done

| № | Вимога | Реалізація та доказ |
| --- | --- | --- |
| 1 | Дані події в Core | `Events/CatalogChangedEventArgs.cs`: Id, Kind, Delta, QuantityAfter, At; get-only властивості; `CatalogChangeKind` |
| 2 | event і повідомлення після успіху | `CatalogService.Changed`, один приватний `OnChanged`, виклики після Add/Update/Remove сховища; помилки та Remove=false не повідомляють |
| 3 | Core не друкує і не пише журнал | У Core немає Console або шляху logs; ConsoleSink/FileSink розташовані в Cli. Наявне JSON-сховище зберігає товари, а не журнал |
| 4 | Два підписники | Локальний `OnCatalogChanged` і збережена лямбда `counter`; вивід [1], [2] показує порядок |
| 5 | Видимий ефект -= | Після відписки першого обробника залишок 70, лічильник 6, журнал без нового рядка; після відписки лямбди залишок 69 і лічильник 6 |
| 6 | Func/Action у сигнатурах | `FindAll(Func<Goods, bool>)`, `GoodsCsvImporter.Load(string, Action<string>?)` |
| 7 | Path.Combine і каталог журналу | Program формує шлях через AppContext.BaseDirectory; FileSink виконує Directory.CreateDirectory перед AppendAllText |
| 8 | README та коміт lab06 | README оновлено; точні команди вибіркового коміту в [docs/lab06-commit.md](docs/lab06-commit.md). Коміт виконує користувач |

## Події складу

| Операція | Kind | Delta | QuantityAfter |
| --- | --- | --- | --- |
| Додати товар з початковим залишком q | Added | +q | q |
| Прихід q | Received | +q | Новий залишок |
| Видача q | Issued | -q | Новий залишок |
| Видалити товар із залишком q | Removed | -q | 0 (товару вже немає в каталозі) |

Видалення не означає операцію фізичної видачі: це вилучення запису з каталогу. Повторне видалення повертає false і не створює події. Нульовий початковий залишок/видалення нульового залишку мають Delta=0.

Подія `event EventHandler<CatalogChangedEventArgs>? Changed` викликається тільки після успішного збереження у сховищі. Підписник уже може прочитати новий стан. Дані події — незмінний знімок, sender — екземпляр сервісу. Find, All, FindAll/Search не створюють подій. Прямі виклики сховища поза сервісом також не піднімають подію сервісу.

## Що показує сценарій

1. Два підписники: локальний метод пише в консоль і app.log; лямбда захоплює `changes` та збільшує його. [1] завжди перед [2].
2. Add(50), Receive(25), Issue(10): залишки 50 → 75 → 65. Додавання тимчасового товару з 3 одиницями й видалення демонструють Removed. Разом **5 подій і 5 рядків app.log** за запуск.
3. Від'ємний прихід, відсутній id, видача понад залишок: повідомлення про помилки, **0 нових подій**, залишок 65. Повторний Remove=false теж без події.
4. `FindAll(p => p.Quantity < 10)` у пам'яті знаходить захисні окуляри з нульовим залишком. У файловому каталозі результат залежить від його даних і може бути порожнім.
5. `-= OnCatalogChanged`, потім Receive(5): залишок 70, лічильник 6. Перший обробник уже не викликається, нового рядка журналу немає.
6. `-= counter`, потім Issue(1): залишок 69, лічильник і журнал без змін. `finally` гарантує відписку і при виході через виняток.

Залишаються звичайні пояснювальні рядки CLI; «тиха операція» означає відсутність повідомлень від знятого обробника. Відписка не зупиняє бізнес-операцію.

## Обидва додаткові завдання

### 1. INotificationSink, ConsoleSink, FileSink, ручний DI

Контракт `INotificationSink.Notify(CatalogChangedEventArgs)` у Core/Abstractions; реалізації — у Cli/Notifications. Конструктор сервісу приймає необов'язковий sink:

```csharp
var service = new CatalogService(new InMemoryCatalogStore(), sink);
```

У додатковому сценарії Cli окремо передає ConsoleSink та FileSink двом сервісам. Підписників Changed у них немає, але кожен sink отримує Added/Received/Issued/Removed. Додаткова демонстрація використовує власні тимчасові сховища в пам'яті й окремий `logs/sink.log` (4 рядки за запуск), тому в app.log видно саме ефект відписки основного сценарію.

Порівняння: подія дозволяє мати 0..N підписників і змінювати їх через += / -=; sink — явна залежність, надана при створенні сервісу. Core в обох випадках не знає про консоль або файл. За одночасної наявності події й sink сервіс спочатку викликає Changed, потім Notify з тим самим об'єктом аргументів. Основний сценарій використовує лише подію, додатковий — лише sink, тому дублювання записів немає.

### 2. Action<string> у методі імпорту

```csharp
public static ImportResult<GoodsDto> Load(string path, Action<string>? onSkipped = null)
```

Для кожного рядка, відхиленого CSV-парсером, імпортер зберігає помилку в Errors і один раз викликає `onSkipped?.Invoke(error)`. Заголовок, порожні рядки та коментарі — не помилки. Старі виклики Load(path) працюють. Лямбда в Cli друкує повідомлення й рахує їх. `data/lab06.csv` дає **3 DTO, 3 помилки (рядки 5, 6, 7), 3 виклики Action**. Дані копіюються до вихідної папки під час збірки; запуск не залежить від поточної директорії.

Це звіт про помилки CSV-парсингу; доменне перетворення GoodsDomainMapper залишається окремим етапом. Відсутній файл — помилка читання, а не «пропущений рядок».

## Журнали, помилки й межі контракту

Шлях основного журналу: `Path.Combine(AppContext.BaseDirectory, "logs", "app.log")`, як у методичці. Для команди вище це **src/Cli/bin/Debug/net10.0/logs/app.log**, а не logs у корені репозиторію. Повний шлях видно в CLI. Журнали дописуються у UTF-8; для останнього запуску використовуйте tail, без видалення старих записів. Git їх ігнорує.

Очікувані помилки файлового запису обробляє сам FileSink: повідомляє в stderr, рахує FailureCount і дозволяє наступному підписнику працювати. CLI завершується кодом 1, якщо журнал не вдалося записати, хоча успішні бізнес-зміни вже збережені. Успішна демонстрація, включно з навмисними відмовами, має код 0. Stack trace не виводиться.

Стандартний event синхронний. Якщо довільний сторонній обробник кине необроблений виняток, наступні обробники та наступний Notify не виконаються, виняток піде до викликача; уже збережена зміна не скасується. Тому ризиковану роботу слід захищати всередині обробника. Action імпорту теж синхронний: необроблена помилка callback перерве імпорт. Автоматичних повторів, транзакцій журналу зі сховищем, асинхронної доставки та багатопотоковості ця навчальна модель не додає.

## Попередні режими сховища

```bash
dotnet run --project src/Cli -f net10.0 --no-build -- --file --path data/catalog.json
dotnet run --project src/Cli -f net10.0 --no-build -- --file --path data/catalog.json --cache --factory
dotnet run --project src/Cli -f net10.0 --no-build -- --file --path data/catalog.json --list-only
```

Кожний звичайний файловий запуск залишає один новий товар DEMO-006 з 69 одиницями. Дані попередніх запусків зберігаються. --list-only нічого не змінює і не пише журналів. Без --file працює пам'ять з 18 початковими товарами. --help показує довідку. Опції --cache/--factory збережені.

## Перевірено 26.10.2026

- `dotnet build CrossApp.slnx`: net8.0 та net10.0, 0 помилок, 0 попереджень.
- **EventChecks: 34 passed, 0 failed** на кожній платформі.
- **ServiceChecks: 45 passed, 0 failed**, **DomainChecks: 56 passed, 0 failed** на кожній платформі.
- **40 перевірок CLI**: обидві платформи, усі комбінації сховищ/фабрики/кешу, повторне збереження, кількість і вміст рядків журналів, list-only, помилки аргументів, пошкоджений JSON, інша робоча директорія, збій журналу, справжній Git Bash.

```bash
dotnet run --project tests/EventChecks -f net10.0 --no-build
dotnet run --project tests/ServiceChecks -f net10.0 --no-build
dotnet run --project tests/DomainChecks -f net10.0 --no-build
```

Це консольні перевірочні проєкти: запускайте dotnet run, не dotnet test. На цьому комп'ютері встановлені runtime .NET 8 і .NET 10; обидва реально перевірено.

## Файли лабораторної 6

| Статус | Файл | Призначення |
| --- | --- | --- |
| + | src/Core/Events/CatalogChangeKind.cs | Види змін складу |
| + | src/Core/Events/CatalogChangedEventArgs.cs | Незмінні дані події |
| ± | src/Core/Services/CatalogService.cs | Changed, OnChanged, FindAll, сповіщення після успіху, sink через DI |
| + | src/Core/Abstractions/INotificationSink.cs | Контракт додаткового завдання |
| + | src/Cli/Notifications/ConsoleSink.cs, FileSink.cs | Реалізації одержувачів, оброблення помилок IO |
| + | src/Cli/Notifications/CatalogNotification.cs | Спільний формат повідомлення |
| ± | src/Core/Import/GoodsCsvImporter.cs | Необов'язковий Action<string> |
| ± | src/Cli/Program.cs | Підписка, відписка, лямбда, відмови, обидва додаткові сценарії |
| + | data/lab06.csv | Контрольний CSV з пошкодженими рядками |
| ± | src/Cli/Cli.csproj | Копіювання CSV під час збірки |
| + | tests/EventChecks/Program.cs, EventChecks.csproj | Нові перевірки |
| ± | CrossApp.slnx | Додавання EventChecks |
| ± | README.md | Поточна лабораторна 6 |
| + | docs/lab06-defense.md, lab06-report.md, lab06-commit.md | Захист, текст звіту, вибірковий коміт |

`+` — новий, `±` — змінений. Вихідних файлів не видалено. Попередні незакомічені зміни користувача в DomainChecks, звітах lab1 і reports не включені до команди коміту.

[Покроковий захист і відповіді](docs/lab06-defense.md) · [Звіт із фактичним виводом та порівнянням sink/event](docs/lab06-report.md) · [Коміт lab06](docs/lab06-commit.md)
