using Core.Configuration;
using Microsoft.Extensions.Configuration;

namespace Core.Tests;

/// <summary>Порядок джерел конфігурації: пізніше джерело перекриває раніше; біндинг у AppOptions.</summary>
public sealed class ConfigurationLayeringTests
{
    [Fact]
    public void LaterSource_OverridesEarlier_AndUntouchedKeysSurvive()
    {
        var file = new Dictionary<string, string?>
        {
            ["Storage:DataPath"] = "data",
            ["Storage:FileName"] = "items.json",
            ["Logging:MinimumLevel"] = "Information"
        };
        var profile = new Dictionary<string, string?> { ["Logging:MinimumLevel"] = "Debug" };
        var overrides = new Dictionary<string, string?> { ["Storage:DataPath"] = "tmp-data" };

        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(file)
            .AddInMemoryCollection(profile)
            .AddInMemoryCollection(overrides)
            .Build();
        AppOptions options = config.Get<AppOptions>()!;

        Assert.Equal("tmp-data", options.Storage.DataPath);
        Assert.Equal("items.json", options.Storage.FileName);
        Assert.Equal("Debug", options.Logging.MinimumLevel);
    }

    [Fact]
    public void Binder_ParsesEnumAndNestedKeys()
    {
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EnvironmentName"] = "Production",
                ["Storage:Location"] = "LocalApplicationData",
                ["Warehouse:DefaultUnit"] = "кг"
            })
            .Build();

        AppOptions options = config.Get<AppOptions>()!;

        Assert.Equal("Production", options.EnvironmentName);
        Assert.Equal(StorageLocation.LocalApplicationData, options.Storage.Location);
        Assert.Equal("кг", options.Warehouse.DefaultUnit);
    }

    [Fact]
    public void EnvironmentVariable_WithDoubleUnderscore_MapsToNestedKey_AndOverridesFile()
    {
        // Окремий префікс, щоб не торкатися справжніх CROSSAPP_* змінних і не заважати іншим тестам.
        const string variable = "CROSSAPPTEST_Storage__DataPath";
        string? previous = Environment.GetEnvironmentVariable(variable);
        try
        {
            Environment.SetEnvironmentVariable(variable, "from-env");

            IConfiguration config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Storage:DataPath"] = "data" })
                .AddEnvironmentVariables(prefix: "CROSSAPPTEST_")
                .Build();

            Assert.Equal("from-env", config["Storage:DataPath"]);
            Assert.Equal("from-env", config.Get<AppOptions>()!.Storage.DataPath);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, previous);
        }
    }

    [Fact]
    public void Secret_IsReadByKey_AndMissingSecretIsNull()
    {
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Integration:ApiKey"] = "local-dev-key" })
            .Build();

        Assert.Equal("local-dev-key", config["Integration:ApiKey"]);
        Assert.Null(config["Integration:Missing"]);
    }
}
