# CrossApp — лабораторна робота 5

**Сервісний шар: інтерфейси, два сховища, ручний DI.** Домен — склад, головна сутність — Goods. Виконано основну частину й усі три додаткові завдання. CLI демонструє лабораторну 5; доменні сутності, DTO та імпортери попередніх робіт використовуються без змін.

## Швидкий запуск у Git Bash

```bash
cd /c/dev/CrossApp
dotnet build CrossApp.slnx
dotnet run --project src/Cli -f net10.0 --no-build
dotnet run --project src/Cli -f net10.0 --no-build -- --file --path data/catalog.json
dotnet run --project src/Cli -f net10.0 --no-build -- --file --path data/catalog.json
cat data/catalog.json
```

У пам'яті кожен запуск починається з 18 початкових товарів і залишає 19. Файлове сховище починається порожнім, якщо файла ще немає, і зберігає товари між запусками: 0 → 1 → 2. Якщо файл вже містить дані, їх не очищують. Кожен сценарій додає один постійний демонстраційний товар із новим Id: залишок 50 → прихід 25 → 75 → видача 10 → 65. Тимчасовий товар додається й видаляється для перевірки Remove.

Звичайний запуск також показує відмови при повторному Id, відсутньому Id та видачі понад залишок. Очікувані винятки перехоплюються, залишок не змінюється. Код успішного сценарію — 0; помилка аргументів, читання/запису або пошкоджений JSON — 1. Виводиться Message без stack trace.

## Архітектура й контракт

```text
Cli/Program.cs (або StoreFactory)
    створює InMemoryCatalogStore або FileCatalogStore
    за потреби обгортає його в CachingCatalogStore
    передає ICatalogStore → CatalogService

CatalogService → ICatalogStore ← InMemoryCatalogStore / FileCatalogStore
                              ← CachingCatalogStore → ICatalogStore
```

`CatalogService(ICatalogStore store)` приймає залежність через конструктор. Він не створює сховища й не працює з Console, файлами або JSON. Бізнес-операції: Add, Receive, Issue; також All, Find, Remove і Search. Доменні правила перевіряє Goods. DI-контейнер і Microsoft.Extensions.DependencyInjection не використовуються.

| Метод ICatalogStore | Призначення й правила |
| --- | --- |
| List() | Знімок усіх товарів у захищеному IReadOnlyList |
| GetById(id) | Товар або null, якщо його немає |
| Add(item) | Новий товар; повторний Id → InvalidOperationException |
| Update(item) | Зберегти зміни існуючого товару; відсутній Id → InvalidOperationException |
| Remove(id) | Видалити; false, якщо такого Id немає |

Id порівнюються через OrdinalIgnoreCase. Отримані та передані сутності копіюються через DTO: змінити повернений Goods можна, але зміни не потраплять у сховище без Update. Read-only список також захищений від Add/Clear через приведення до IList.

## Реалізації та файл

- **InMemoryCatalogStore** — Dictionary, необов'язкові початкові дані через конструктор. `SampleData.Goods()` створює 18 незалежних товарів для демонстрації та наступних лабораторних.
- **FileCatalogStore** — відкладене завантаження JSON при першому зверненні, кеш у пам'яті та запис після кожного успішного Add/Update/Remove. Відсутній файл означає порожнє сховище. JSON — масив GoodsDto з Id, Sku, Name, Unit, Quantity, Note. Відновлення через Goods.FromDto перевіряє інваріанти.

Без `--path` використовується `Path.Combine(AppContext.BaseDirectory, "data", "catalog.json")`, як у прикладі методички. Для Debug/net10.0 це **src/Cli/bin/Debug/net10.0/data/catalog.json**. Для файла **data/catalog.json у корені репозиторію** використовуйте `--file --path data/catalog.json`, як у командах вище. Повний шлях завжди показує CLI; поточна робоча папка впливає лише на явно переданий відносний --path.

Пошкоджений JSON, некоректний DTO або дублікати Id у файлі відхиляються без перезаписування оригіналу. Новий стан спочатку записується в тимчасовий файл у тій самій папці, потім файл замінюється; кеш оновлюється після успішного запису. Невдалий запис не змінює кеш.

Це синхронні сховища для одного процесу/потоку. Вони не забезпечують транзакцій між кількома екземплярами або автоматичного перечитування зовнішніх змін. Новий процес читає актуальний JSON. Екранування кирилиці як `\u....` — стандартна поведінка System.Text.Json; при читанні текст відновлюється.

## Усі додаткові завдання

1. **CachingCatalogStore(ICatalogStore inner)** — декоратор з тим самим контрактом. Перше List/GetById отримує знімок inner.List(); наступні читання використовують кеш. Успішні зміни передаються inner й скидають кеш. Тест зі CountingStore підтверджує зменшення кількості звернень і правильне оновлення після запису.
2. **Search(Func<Goods, bool>)** у CatalogService — фільтрація за переданою функцією. CLI шукає товари з одиницею «м» і залишком не менше 60; цей сценарій є в кожному звичайному запуску.
3. **StoreFactory.Create(args)** у Cli/Composition — альтернативне створення сховища. Без --factory збережено тернарний вибір у Program.cs, із --factory викликається фабрика. Конкретні залежності залишаються на стороні Cli; фабрика розширює composition root для додаткового завдання. Сервіс однаковий в обох режимах.

```bash
dotnet run --project src/Cli -f net10.0 --no-build -- --cache
dotnet run --project src/Cli -f net10.0 --no-build -- --factory
dotnet run --project src/Cli -f net10.0 --no-build -- --file --path data/catalog.json --cache --factory
```

## Аргументи CLI

| Аргумент | Дія |
| --- | --- |
| --file | Вибрати JSON-сховище замість пам'яті |
| --path шлях | Явно задати файл; тільки разом із --file |
| --cache | Обгорнути вибране сховище декоратором |
| --factory | Створити залежності через StoreFactory |
| --list-only | Показати наявні товари без змін |
| --help | Показати довідку; використовується окремо |

## Перевірено

```bash
dotnet run --project tests/ServiceChecks -f net10.0 --no-build
dotnet run --project tests/DomainChecks -f net10.0 --no-build
```

- Збірка всього рішення для net8.0 і net10.0: 0 помилок, 0 попереджень.
- Окрема контрольна збірка Core без FileCatalogStore також успішна для обох платформ: сервіс не залежить від файлової реалізації.
- ServiceChecks: **45 пройдено, 0 помилок** на Windows (44 на інших ОС: перевірка блокування файлу Windows-specific).
- Попередні DomainChecks: **56 пройдено, 0 помилок**.
- 17 перевірок запуску CLI: обидва режими, повторні запуски, читання без змін, усі комбінації фабрики/кешу, пошкоджений JSON, неправильні аргументи, довідка та Git Bash.

Запуск перевірено на .NET 10. Для запуску net8.0 потрібен .NET 8 Runtime, якого на перевіреному комп'ютері немає. Проєкти тестів — консольні застосунки без стороннього тестового фреймворку; використовуйте dotnet run, а не dotnet test.

## Змінені та нові файли лабораторної 5

| Статус | Файли | Вимога |
| --- | --- | --- |
| + | src/Core/Abstractions/ICatalogStore.cs | Контракт сховища |
| + | src/Core/Storage/InMemoryCatalogStore.cs | Сховище в пам'яті |
| + | src/Core/Storage/FileCatalogStore.cs | JSON, завантаження й збереження |
| + | src/Core/Storage/CatalogData.cs | Спільні правила ключів, копіювання та колекцій |
| + | src/Core/Services/CatalogService.cs | Сервіс, DI, Add/Receive/Issue та пошук із Func |
| + | src/Core/SampleData.cs | 18 початкових записів |
| + | src/Core/Storage/CachingCatalogStore.cs | Додаткове: декоратор |
| + | src/Cli/Composition/StoreFactory.cs | Додаткове: фабрика |
| + | src/Cli/Composition/StoreOptions.cs | Перевірка аргументів CLI |
| ± | src/Cli/Program.cs | Composition root, перемикання режимів, демонстрація |
| + | tests/ServiceChecks/Program.cs, ServiceChecks.csproj | Нові перевірки сховищ, сервісу, кешу й фабрики |
| ± | CrossApp.slnx | Новий перевірочний проєкт у рішенні |
| ± | .gitignore | Не комітити локальний data/catalog.json |
| ± | README.md | Опис лабораторної 5 |
| + | docs/lab05-defense.md | Промова, теорія, команди, 10 відповідей |
| + | docs/lab05-report.md | Звіт за структурою методички з фактичним виводом |
| + | docs/lab05-commit.md | Вибірковий git add і команда коміту lab05 |

`+` — новий, `±` — змінений. Видалених вихідних файлів немає. Попередні незакомічені зміни в DomainChecks і звітах не змінювалися.

Докладна підготовка: [захист](docs/lab05-defense.md), [звіт](docs/lab05-report.md), [коміт lab05](docs/lab05-commit.md). Коміт створює користувач за наведеними командами; автоматичного коміту чи push не виконувалося.
