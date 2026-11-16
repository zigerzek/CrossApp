namespace Core.Configuration;

/// <summary>
/// Типізовані налаштування застосунку. Заповнюються через <c>config.Get&lt;AppOptions&gt;()</c>
/// з appsettings.json, appsettings.{Environment}.json, user-secrets, змінних середовища
/// та аргументів командного рядка (пізніше джерело перекриває раніше).
/// Секрети (наприклад, Integration:ApiKey) сюди свідомо не потрапляють:
/// вони читаються напряму як <c>config["Integration:ApiKey"]</c>.
/// </summary>
public sealed class AppOptions
{
    public const string DefaultEnvironment = "Development";

    public string EnvironmentName { get; init; } = DefaultEnvironment;
    public LoggingOptions Logging { get; init; } = new();
    public StorageOptions Storage { get; init; } = new();
    public WarehouseOptions Warehouse { get; init; } = new();
}

/// <summary>Розділ "Logging" у конфігурації.</summary>
public sealed class LoggingOptions
{
    /// <summary>Trace, Debug, Information, Warning або Error.</summary>
    public string MinimumLevel { get; init; } = "Information";
}

/// <summary>Розділ "Warehouse" — параметри предметної області (варіант «Склад»).</summary>
public sealed class WarehouseOptions
{
    public string DefaultUnit { get; init; } = "шт";
}

/// <summary>Від чого відраховується відносний <see cref="StorageOptions.DataPath"/>.</summary>
public enum StorageLocation
{
    /// <summary>Каталог застосунку (<c>AppContext.BaseDirectory</c>) — зручно для розробки.</summary>
    BaseDirectory,

    /// <summary>Системний каталог даних користувача (<c>SpecialFolder.LocalApplicationData</c>) — для розгорнутого застосунку.</summary>
    LocalApplicationData
}

/// <summary>Розділ "Storage": де лежить файл даних.</summary>
public sealed class StorageOptions
{
    public const string AppFolderName = "CrossApp";

    public string DataPath { get; init; } = "data";
    public string FileName { get; init; } = "items.json";
    public StorageLocation Location { get; init; } = StorageLocation.BaseDirectory;

    /// <summary>
    /// Каталог даних без створення на диску. Абсолютний <see cref="DataPath"/> береться як є,
    /// відносний комбінується через <see cref="Path.Combine(string, string)"/> з базовим каталогом
    /// (залежно від <see cref="Location"/>), а не з поточним робочим каталогом процесу.
    /// </summary>
    public string ResolveDirectory(string baseDir)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDir);
        if (Path.IsPathRooted(DataPath))
            return Path.GetFullPath(DataPath);

        string anchor = baseDir;
        if (Location == StorageLocation.LocalApplicationData)
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            // Порожній рядок (ОС не надала каталог) → лишаємось у каталозі застосунку, а не в поточному робочому.
            if (!string.IsNullOrEmpty(local))
                anchor = Path.Combine(local, AppFolderName);
        }

        return Path.GetFullPath(Path.Combine(anchor, DataPath));
    }

    /// <summary>Повний шлях до файлу даних; каталог створюється, якщо його ще немає.</summary>
    public string ResolveFullPath(string baseDir)
    {
        if (string.IsNullOrWhiteSpace(FileName) || !string.Equals(Path.GetFileName(FileName), FileName, StringComparison.Ordinal))
            throw new ArgumentException(
                $"Storage:FileName має бути іменем файлу без каталогів, а не '{FileName}'.", nameof(FileName));

        string root = ResolveDirectory(baseDir);
        Directory.CreateDirectory(root);
        return Path.GetFullPath(Path.Combine(root, FileName));
    }
}
