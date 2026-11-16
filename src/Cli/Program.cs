using Cli;
using Cli.Commands;
using Cli.Configuration;
using Cli.Logging;
using Core;

Console.OutputEncoding = System.Text.Encoding.UTF8;
if (args is ["--help"] or ["-h"])
{
    HelpText.Print();
    return 0;
}

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true; // кооперативне скасування: операції самі перевіряють токен
    cts.Cancel();
};

CliArguments cli = CliArguments.Parse(args);

try
{
    AppSettings settings = AppConfiguration.Load(cli.ConfigArgs);
    var ctx = new CommandContext(settings, ConsoleLog.FromOptions(settings.Options.Logging.MinimumLevel), cli, cts.Token);

    string[] rest = cli.Rest;
    string? first = rest.Length > 0 ? rest[0] : null;

    // Лаб. 8: --file — асинхронний імпорт у пам'ять (поведінку збережено).
    if (rest.Contains("--file"))
        return await ImportCommand.RunAsync(ctx, persistent: false);

    // Лаб. 9: import — асинхронний імпорт у файл даних; export/list/add/config — робота з файлом даних.
    if (rest.Contains("import"))
        return await ImportCommand.RunAsync(ctx, persistent: true);
    if (first is not null && DataCommands.Names.Contains(first))
        return DataCommands.Run(first, ctx);

    // Лаб. 5–7: сховища та LINQ-звіти.
    return ReportCommands.Run(ctx);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Операцію скасовано.");
    return 0;
}
catch (DomainException ex)
{
    Console.WriteLine($"Дані відхилено: {ex.Message}");
    return 1;
}
catch (FileNotFoundException ex)
{
    Console.WriteLine($"Файл не знайдено: {ex.FileName ?? ex.Message}");
    return 1;
}
catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException
                              or ArgumentException or InvalidOperationException or NotSupportedException
                              or System.Text.Json.JsonException)
{
    // InvalidDataException — зіпсований JSON (сховище або appsettings): зрозуміле повідомлення без stack trace.
    Console.Error.WriteLine($"Помилка: {ex.Message}");
    return 1;
}
