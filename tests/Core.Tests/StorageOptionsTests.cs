using Core.Configuration;

namespace Core.Tests;

public sealed class StorageOptionsTests
{
    private static string NewTempDir() => Path.Combine(Path.GetTempPath(), $"crossapp_opts_{Guid.NewGuid():N}");

    [Fact]
    public void ResolveFullPath_RelativeDataPath_IsCombinedWithBaseDir_AndDirectoryIsCreated()
    {
        string baseDir = NewTempDir();
        try
        {
            var options = new StorageOptions { DataPath = "data", FileName = "items.json" };

            string path = options.ResolveFullPath(baseDir);

            Assert.Equal(Path.GetFullPath(Path.Combine(baseDir, "data", "items.json")), path);
            Assert.True(Directory.Exists(Path.Combine(baseDir, "data")));
        }
        finally
        {
            if (Directory.Exists(baseDir)) Directory.Delete(baseDir, recursive: true);
        }
    }

    [Fact]
    public void ResolveFullPath_RootedDataPath_IgnoresBaseDir()
    {
        string root = NewTempDir();
        string absolute = Path.Combine(root, "elsewhere");
        try
        {
            var options = new StorageOptions { DataPath = absolute, FileName = "x.json" };

            string path = options.ResolveFullPath(Path.Combine(root, "ignored-base"));

            Assert.Equal(Path.GetFullPath(Path.Combine(absolute, "x.json")), path);
            Assert.False(Directory.Exists(Path.Combine(root, "ignored-base")));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ResolveDirectory_LocalApplicationData_StartsFromUserDataFolder_WithoutCreatingIt()
    {
        var options = new StorageOptions { DataPath = "data", Location = StorageLocation.LocalApplicationData };

        string directory = options.ResolveDirectory(Path.Combine(Path.GetTempPath(), "unused-base"));

        string expected = Path.GetFullPath(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            StorageOptions.AppFolderName, "data"));
        Assert.Equal(expected, directory);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("sub/items.json")]
    [InlineData("../items.json")]
    public void ResolveFullPath_FileNameWithDirectories_IsRejected(string fileName)
    {
        var options = new StorageOptions { FileName = fileName };

        Assert.Throws<ArgumentException>(() => options.ResolveFullPath(Path.GetTempPath()));
    }

    [Fact]
    public void Defaults_MatchDocumentedValues()
    {
        var app = new AppOptions();

        Assert.Equal("Development", app.EnvironmentName);
        Assert.Equal("data", app.Storage.DataPath);
        Assert.Equal("items.json", app.Storage.FileName);
        Assert.Equal(StorageLocation.BaseDirectory, app.Storage.Location);
        Assert.Equal("шт", app.Warehouse.DefaultUnit);
    }
}
