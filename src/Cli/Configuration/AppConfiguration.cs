using Core.Configuration;
using Microsoft.Extensions.Configuration;

namespace Cli.Configuration;

/// <summary>Результат збирання конфігурації: усе, що потрібно командам CLI.</summary>
internal sealed record AppSettings(
    IConfiguration Configuration,
    AppOptions Options,
    string Profile,
    string BaseDirectory,
    string DataFilePath,
    string? DataPathOverride,
    string? ApiKey)
{
    /// <summary>Секрет ніколи не друкується — лише факт наявності й довжина.</summary>
    public string ApiKeyStatus => ApiKey is null
        ? "ApiKey: не заданий"
        : $"ApiKey: присутній, довжина {ApiKey.Length}";
}

internal static class AppConfiguration
{
    public const string ProfileVariable = "CROSSAPP_ENVIRONMENT";
    public const string EnvironmentPrefix = "CROSSAPP_";
    public const string DataPathVariable = "CROSSAPP_DATA_PATH";
    public const string ApiKeyKey = "Integration:ApiKey";

    /// <summary>
    /// Порядок джерел (кожне наступне перекриває попередні):
    /// appsettings.json → appsettings.{Profile}.json → user-secrets → CROSSAPP_* → аргументи
    /// командного рядка (<c>--Storage:DataPath=tmp</c>). Окремо підтримано просту змінну
    /// CROSSAPP_DATA_PATH, яка перекриває Storage:DataPath.
    /// </summary>
    public static AppSettings Load(string[] commandLineArgs, string? baseDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(commandLineArgs);
        string baseDir = baseDirectory ?? AppContext.BaseDirectory;
        string profile = Environment.GetEnvironmentVariable(ProfileVariable) is { Length: > 0 } fromEnv
            ? fromEnv
            : AppOptions.DefaultEnvironment;

        IConfiguration config = new ConfigurationBuilder()
            .SetBasePath(baseDir)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile($"appsettings.{profile}.json", optional: true, reloadOnChange: false)
            .AddUserSecrets(typeof(AppConfiguration).Assembly, optional: true)
            .AddEnvironmentVariables(EnvironmentPrefix)
            .AddCommandLine(commandLineArgs)
            .Build();

        AppOptions options = config.Get<AppOptions>() ?? new AppOptions();

        string? simpleOverride = Environment.GetEnvironmentVariable(DataPathVariable);
        string? dataPathOverride = string.IsNullOrWhiteSpace(simpleOverride) ? null : simpleOverride;
        var storage = new StorageOptions
        {
            DataPath = dataPathOverride ?? options.Storage.DataPath,
            FileName = options.Storage.FileName,
            Location = options.Storage.Location
        };

        string? apiKey = config[ApiKeyKey];
        return new AppSettings(
            config,
            options,
            profile,
            baseDir,
            storage.ResolveFullPath(baseDir),
            dataPathOverride,
            string.IsNullOrEmpty(apiKey) ? null : apiKey);
    }
}
