namespace Cli.Composition;

public sealed record StoreOptions(bool UseFile, bool UseCache, bool UseFactory, bool ListOnly, string DataPath)
{
    public static StoreOptions Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        bool file = false, cache = false, factory = false, listOnly = false;
        string? path = null;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--file": file = true; break;
                case "--cache": cache = true; break;
                case "--factory": factory = true; break;
                case "--list-only": listOnly = true; break;
                case "--path":
                    if (path is not null || ++i == args.Length || string.IsNullOrWhiteSpace(args[i]) || args[i].StartsWith("--"))
                        throw new ArgumentException("Після --path укажіть один шлях до JSON-файлу.");
                    path = args[i];
                    break;
                default: throw new ArgumentException($"Невідомий аргумент: {args[i]}. Довідка: --help.");
            }
        }

        if (path is not null && !file)
            throw new ArgumentException("Параметр --path використовується разом із --file.");
        return new(file, cache, factory, listOnly,
            Path.GetFullPath(path ?? Path.Combine(AppContext.BaseDirectory, "data", "catalog.json")));
    }
}
