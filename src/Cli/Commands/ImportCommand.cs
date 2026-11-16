using System.Text.Json;
using Cli.Composition;
using Core;
using Core.Abstractions;
using Core.Services;
using Core.Storage;

namespace Cli.Commands;

/// <summary>
/// Асинхронний імпорт JSON (лаб. 8: async/await, CancellationToken, DomainException, stream, progress).
/// <c>--file</c> імпортує в пам'ять, <c>import</c> (лаб. 9) — у файл даних із конфігурації.
/// </summary>
internal static class ImportCommand
{
    public static async Task<int> RunAsync(CommandContext ctx, bool persistent)
    {
        (string _, string[] storeArguments) = ArgumentSplitter.Split(ctx.Args.Rest);
        // Для команди import прапорець --file зайвий: --path <файл> достатньо.
        if (persistent && !storeArguments.Contains("--file"))
            storeArguments = ["--file", .. storeArguments];
        StoreOptions options = StoreOptions.Parse(storeArguments);

        string dataPath = options.DataPath;
        bool hasCustomPath = ctx.Args.Rest.Contains("--path");
        string target = persistent ? ctx.Settings.DataFilePath : "у пам'ять";

        // Без --path команда import читає файл даних (якщо він уже є), інакше — вбудований каталог.
        if (persistent && !hasCustomPath && File.Exists(ctx.Settings.DataFilePath))
            dataPath = ctx.Settings.DataFilePath;

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ctx.Ct);
        if (ctx.Args.Timeout.HasValue) linked.CancelAfter(ctx.Args.Timeout.Value);
        if (ctx.Args.ImmediateCancel) linked.Cancel();

        if (persistent)
        {
            Console.WriteLine($"Файл даних : {ctx.Settings.DataFilePath}");
            Console.WriteLine(ctx.Settings.ApiKeyStatus);
            ctx.Log.Debug($"Джерело імпорту: {dataPath}");
        }

        try
        {
            ICatalogStore store = persistent
                ? new FileCatalogStore(ctx.Settings.DataFilePath)
                : new InMemoryCatalogStore();
            var service = new CatalogService(store);
            // import (лаб. 9) — це імпорт колекції: нульовий залишок допустимий; --file (лаб. 8) лишається суворим.
            var importer = new JsonCatalogImporter(allowZeroQuantity: persistent);

            IProgress<int>? progress = ctx.Args.ShowProgress
                ? new Progress<int>(percent => Console.Write($"\rПрогрес імпорту: {percent,3}%"))
                : null;

            int added = ctx.Args.UseStream
                ? await service.ImportStreamAsync(importer, dataPath, progress, linked.Token)
                : await service.ImportAsync(importer, dataPath, progress, linked.Token);

            if (ctx.Args.ShowProgress)
                Console.WriteLine();

            Console.WriteLine($"Імпортовано записів: {added}");
            if (persistent)
                ctx.Log.Debug($"Усього у {target}: {store.List().Count}");
            return 0;
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Імпорт скасовано.");
            return 0;
        }
        catch (DomainException ex)
        {
            Console.WriteLine($"Дані відхилено: {ex.Message}");
            return 1;
        }
        catch (FileNotFoundException)
        {
            Console.WriteLine($"Файл не знайдено: {dataPath}");
            return 1;
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"Файл {Path.GetFileName(dataPath)} містить некоректний JSON: {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Помилка: {ex.Message}");
            return 1;
        }
    }
}
