namespace Cli.Commands;

internal static class HelpText
{
    public static void Print()
    {
        Console.WriteLine("CrossApp — Warehouse (лаб. 9: конфігурація, шляхи, JSON, secrets; лаб. 5–8 збережено)");
        Console.WriteLine();
        Console.WriteLine("Лаб. 9 — дані та конфігурація:");
        Console.WriteLine("  export [--out <файл.json>]   записати всю колекцію у JSON (за замовчуванням — файл даних із конфігурації)");
        Console.WriteLine("  import [--path <файл.json>]  асинхронно імпортувати JSON у файл даних (--stream | --progress | --cancel | --timeout <ms/s>)");
        Console.WriteLine("  list                         показати вміст файлу даних");
        Console.WriteLine("  add <sku> <назва> <к-сть> [одиниця]  додати товар (одиниця за замовчуванням — Warehouse:DefaultUnit)");
        Console.WriteLine("  config                       показати активну конфігурацію (секрет — лише факт наявності)");
        Console.WriteLine("  Перевизначення: CROSSAPP_ENVIRONMENT, CROSSAPP_DATA_PATH, CROSSAPP_Storage__DataPath, --Storage:DataPath=<шлях>");
        Console.WriteLine();
        Console.WriteLine("Лаб. 8 — асинхронний імпорт у пам'ять:");
        Console.WriteLine("  --file [--path <файл.json>] [--stream] [--cancel] [--timeout <ms/s>] [--progress]");
        Console.WriteLine();
        Console.WriteLine("Лаб. 7 — звіти (LINQ):");
        Console.WriteLine("  report-top | report-low | report-groups | report-sum | report-batches | report-top-batches | report-benchmark");
        Console.WriteLine("  Параметри сховища (лаб. 5): --file --cache --factory --list-only --path <файл.json>");
    }
}
