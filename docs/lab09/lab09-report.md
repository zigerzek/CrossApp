# Звіт до лабораторної роботи 9

**Тема:** шляхи, JSON, конфігурація, secrets. **Домен:** Склад (товари `id, sku, name, unit, quantity` + ключ `Warehouse:DefaultUnit`).

> Виводи нижче — приклад. Для здачі виконайте `bash scripts/lab09-demo.sh` і вставте власні виводи зі своїми шляхами.

## 1. Профілі Development і Production

Профіль вибирає змінна `CROSSAPP_ENVIRONMENT` (за замовчуванням `Development`); файл `appsettings.{профіль}.json` перекриває `appsettings.json`.

| | Development | Production |
| --- | --- | --- |
| Файл | `appsettings.Development.json` | `appsettings.Production.json` |
| `Logging:MinimumLevel` | `Debug` — у консолі з'являються рядки `[debug] …` | `Warning` — `[debug]`/`[information]` приховано |
| `Storage:Location` | `BaseDirectory` (за базою) | `LocalApplicationData` |
| Файл даних | `<репозиторій>\src\Cli\bin\Debug\net10.0\data\items.json` | `%LOCALAPPDATA%\CrossApp\data\items.json` |

Чому так: під час розробки дані зручно тримати поруч зі збіркою і чистити разом із `bin/`; у розгорнутому застосунку каталог програми може бути недоступним для запису, тому дані живуть у системному каталозі користувача.

## 2. Фрагмент з `Path.Combine`

```csharp
// src/Core/Configuration/AppOptions.cs
public string ResolveFullPath(string baseDir)
{
    string root = ResolveDirectory(baseDir);          // Path.Combine(anchor, DataPath) → Path.GetFullPath
    Directory.CreateDirectory(root);
    return Path.GetFullPath(Path.Combine(root, FileName));
}
```

**Чому не конкатенація рядків** (`baseDir + "\\" + "data"`): роздільник каталогів залежить від ОС (`\` у Windows, `/` у Linux/macOS), а ручне склеювання дає подвійні/відсутні роздільники й ламається на іншій платформі. `Path.Combine` підставляє правильний роздільник, а `Path.GetFullPath` нормалізує `..` і `.`. Базою служить `AppContext.BaseDirectory` (каталог зібраних файлів), а не `Environment.CurrentDirectory`, який залежить від способу запуску — тому запуск із кореня solution і з `src/Cli` дає однаковий файл. Додатково `Storage:FileName` не може містити каталоги (інакше `Path.Combine` проігнорував би `root`).

## 3. Вивід до і після `CROSSAPP_DATA_PATH`; розташування user-secrets

До:

```
> dotnet run --project src/Cli -- config
…
CROSSAPP_DATA_PATH    : (не задано)
Файл даних            : <репозиторій>\src\Cli\bin\Debug\net10.0\data\items.json
```

Після (`CROSSAPP_DATA_PATH=C:/temp/crossapp-data`):

```
> dotnet run --project src/Cli -- export
Файл даних : C:\temp\crossapp-data\items.json
ApiKey: присутній, довжина 13
[information] Файл даних порожній — експортуємо демонстраційний каталог (SampleData).
Експортовано 18 записів.
```

Новий файл `C:\temp\crossapp-data\items.json` відкривається в редакторі: camelCase-ключі, кирилиця читабельна (не `\u0426`).

**user-secrets на Windows:** `%APPDATA%\Microsoft\UserSecrets\78e79000-18df-407f-ad51-0c2efa4a8b9d\secrets.json`, тобто `C:\Users\<логін>\AppData\Roaming\Microsoft\UserSecrets\…\secrets.json`; `UserSecretsId` береться з `src/Cli/Cli.csproj` і комітиться, значення — ні. Linux/macOS: `~/.microsoft/usersecrets/<UserSecretsId>/secrets.json`.

## 4. Самоперевірка

1. `git grep -nE 'C:\\|/home/' -- src` — порожньо.
2. Запуск із двох каталогів дає той самий файл (`AppContext.BaseDirectory`).
3. Після видалення `data/` каталог створюється заново (`Directory.CreateDirectory` у `ResolveFullPath`/`WriteAtomic`).
4. Зіпсований JSON → `Помилка: Некоректний каталог '…items.json': …` без stack trace (`InvalidDataException`).
5. `git status` після `user-secrets set` чистий; кирилиця у файлі без `\uXXXX` (`UnsafeRelaxedJsonEscaping`).

## 5. Відповіді на питання для захисту

1. **`CurrentDirectory` vs `BaseDirectory`.** `Environment.CurrentDirectory` — каталог, з якого запущено процес (залежить від способу запуску); `AppContext.BaseDirectory` — каталог зібраного застосунку, не залежить від cwd.
2. **Чому `Path.Combine`.** Платформний роздільник, коректна обробка зайвих роздільників, менше помилок, ніж у ручної конкатенації.
3. **`SpecialFolder.LocalApplicationData`.** Для даних розгорнутого застосунку, що належать користувачу й мають переживати оновлення програми (`Storage:Location = LocalApplicationData`).
4. **Порядок джерел.** `appsettings.json` → `appsettings.{Environment}.json` → user-secrets → змінні середовища → командний рядок; пізніше джерело перекриває раніше.
5. **Ключ у змінній середовища.** `Storage:DataPath` → `Storage__DataPath`, з префіксом — `CROSSAPP_Storage__DataPath`.
6. **Де user-secrets і чому не для продакшену.** У профілі користувача поза репозиторієм (див. вище); це нешифрований JSON для локальної розробки. У продакшені — змінні середовища або сховище секретів.
7. **`PropertyNamingPolicy = CamelCase`.** Під час запису `Sku` → `sku`; під час читання імена зіставляються за політикою (у нас додатково `PropertyNameCaseInsensitive`, тому старі PascalCase-файли теж читаються).
8. **Кирилиця й `\uXXXX`.** За замовчуванням серіалізатор екранує не-ASCII; дані не втрачаються, але файл нечитабельний. `JavaScriptEncoder.UnsafeRelaxedJsonEscaping` вимикає це; обережно, якщо JSON вбудовується в HTML.
9. **`.tmp` + `File.Move`.** Запис у тимчасовий файл і атомарна заміна гарантують, що збій посеред запису не залишить напівзаписаний файл даних.
