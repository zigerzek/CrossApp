using Cli.Configuration;
using Core;
using Core.Domain;
using Core.Services;
using Core.Storage;

namespace Cli.Commands;

/// <summary>Команди лаб. 9: export / list / add / config над файлом даних із конфігурації.</summary>
internal static class DataCommands
{
    private const int PreviewLines = 12;

    public static readonly string[] Names = ["export", "list", "add", "config"];

    public static int Run(string command, CommandContext ctx)
    {
        return command switch
        {
            "export" => Export(ctx),
            "list" => List(ctx),
            "add" => Add(ctx),
            "config" => ShowConfig(ctx),
            _ => throw new ArgumentException($"Невідома команда: {command}.")
        };
    }

    private static int Export(CommandContext ctx)
    {
        AppSettings settings = ctx.Settings;
        PrintHeader(settings);

        var store = new FileCatalogStore(settings.DataFilePath);
        IReadOnlyList<Goods> items = store.List();
        if (items.Count == 0)
        {
            ctx.Log.Info("Файл даних порожній — експортуємо демонстраційний каталог (SampleData).");
            items = SampleData.Goods();
        }

        string target = settings.DataFilePath;
        string[] rest = ctx.Args.Rest;
        int outIndex = Array.IndexOf(rest, "--out");
        if (outIndex >= 0)
        {
            if (outIndex + 1 >= rest.Length || rest[outIndex + 1].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException("Після --out укажіть шлях до JSON-файлу.");
            target = Path.GetFullPath(rest[outIndex + 1]);
        }

        CatalogJson.WriteAtomic(target, items);
        ctx.Log.Debug($"Атомарний запис: .tmp + File.Move → {target}");
        Console.WriteLine($"Експортовано {items.Count} {Plural(items.Count)}.");
        PrintPreview(target);
        return 0;
    }

    private static int List(CommandContext ctx)
    {
        PrintHeader(ctx.Settings);
        IReadOnlyList<Goods> items = new FileCatalogStore(ctx.Settings.DataFilePath).List();
        if (items.Count == 0)
            Console.WriteLine("Файл даних порожній або відсутній. Виконайте export або import.");
        foreach (Goods item in items)
            Console.WriteLine($"  {item}");
        Console.WriteLine($"Усього: {items.Count} {Plural(items.Count)}.");
        return 0;
    }

    private static int Add(CommandContext ctx)
    {
        // rest: add <sku> <назва> <кількість> [одиниця]
        string[] positional = ctx.Args.Rest.Skip(1).ToArray();
        if (positional.Length is < 3 or > 4)
            throw new ArgumentException("Використання: add <sku> <назва> <кількість> [одиниця].");
        if (!int.TryParse(positional[2], out int quantity))
            throw new ArgumentException($"Кількість має бути цілим числом, а не '{positional[2]}'.");

        string unit = positional.Length == 4 ? positional[3] : ctx.Settings.Options.Warehouse.DefaultUnit;
        PrintHeader(ctx.Settings);

        var service = new CatalogService(new FileCatalogStore(ctx.Settings.DataFilePath));
        Goods item = service.Add(positional[0], positional[1], unit, quantity);
        Console.WriteLine($"Додано: {item}");
        return 0;
    }

    private static int ShowConfig(CommandContext ctx)
    {
        AppSettings s = ctx.Settings;
        Console.WriteLine($"Профіль               : {s.Profile} (EnvironmentName = {s.Options.EnvironmentName})");
        Console.WriteLine($"Logging:MinimumLevel  : {s.Options.Logging.MinimumLevel}");
        Console.WriteLine($"Storage:DataPath      : {s.Options.Storage.DataPath}");
        Console.WriteLine($"Storage:FileName      : {s.Options.Storage.FileName}");
        Console.WriteLine($"Storage:Location      : {s.Options.Storage.Location}");
        Console.WriteLine($"Warehouse:DefaultUnit : {s.Options.Warehouse.DefaultUnit}");
        Console.WriteLine($"CROSSAPP_DATA_PATH    : {s.DataPathOverride ?? "(не задано)"}");
        Console.WriteLine($"Файл даних            : {s.DataFilePath}");
        Console.WriteLine($"Каталог застосунку    : {s.BaseDirectory}");
        Console.WriteLine($"Поточний каталог      : {Environment.CurrentDirectory}");
        Console.WriteLine(s.ApiKeyStatus);
        return 0;
    }

    private static void PrintHeader(AppSettings settings)
    {
        Console.WriteLine($"Файл даних : {settings.DataFilePath}");
        Console.WriteLine(settings.ApiKeyStatus);
    }

    private static void PrintPreview(string path)
    {
        string[] lines = File.ReadAllLines(path);
        Console.WriteLine($"{path}:");
        foreach (string line in lines.Take(PreviewLines))
            Console.WriteLine(line);
        if (lines.Length > PreviewLines)
            Console.WriteLine($"... (ще {lines.Length - PreviewLines} рядків)");
    }

    /// <summary>Узгодження числівника: 1 запис, 2–4 записи, 0 і 5+ записів.</summary>
    internal static string Plural(int count)
    {
        int lastTwo = count % 100, last = count % 10;
        if (lastTwo is >= 11 and <= 14) return "записів";
        return last switch { 1 => "запис", >= 2 and <= 4 => "записи", _ => "записів" };
    }
}
