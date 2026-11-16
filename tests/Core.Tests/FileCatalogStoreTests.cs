using Core.Domain;
using Core.Storage;

namespace Core.Tests;

/// <summary>JSON-формат файлового сховища: camelCase, читабельна кирилиця, атомарний запис, зрозумілі помилки.</summary>
public sealed class FileCatalogStoreTests
{
    private static string NewDir() => Path.Combine(Path.GetTempPath(), $"crossapp_store_{Guid.NewGuid():N}");

    private static void Cleanup(string dir)
    {
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
    }

    [Fact]
    public void Add_WritesCamelCaseJson_WithReadableCyrillic_AndRoundTrips()
    {
        string dir = NewDir();
        try
        {
            string path = Path.Combine(dir, "nested", "items.json");
            new FileCatalogStore(path).Add(Goods.Create("G-1", "sku-1", "Цемент М400", "мішок", 40));

            string json = File.ReadAllText(path);
            Assert.Contains("\"sku\"", json);
            Assert.DoesNotContain("\"Sku\"", json);
            Assert.Contains("Цемент М400", json);
            Assert.DoesNotContain("\\u0426", json);
            Assert.DoesNotContain("\"note\"", json); // null не записується

            Goods loaded = new FileCatalogStore(path).GetById("G-1")!;
            Assert.Equal("Цемент М400", loaded.Name);
            Assert.Equal("SKU-1", loaded.Sku);
            Assert.Equal(40, loaded.Quantity);
        }
        finally { Cleanup(dir); }
    }

    [Fact]
    public void Commit_LeavesNoTemporaryFiles()
    {
        string dir = NewDir();
        try
        {
            var store = new FileCatalogStore(Path.Combine(dir, "items.json"));
            store.Add(Goods.Create("G-1", "S1", "Один", "шт", 1));
            store.Add(Goods.Create("G-2", "S2", "Два", "шт", 2));
            store.Remove("G-1");

            Assert.Empty(Directory.GetFiles(dir, "*.tmp"));
            Assert.Single(Directory.GetFiles(dir));
        }
        finally { Cleanup(dir); }
    }

    [Fact]
    public void List_ReadsLegacyPascalCaseFiles()
    {
        string dir = NewDir();
        try
        {
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "legacy.json");
            File.WriteAllText(path, """[{"Id":"G-9","Sku":"S9","Name":"Старий формат","Unit":"шт","Quantity":3}]""");

            var items = new FileCatalogStore(path).List();

            Assert.Single(items);
            Assert.Equal("Старий формат", items[0].Name);
        }
        finally { Cleanup(dir); }
    }

    [Fact]
    public void List_WithCorruptedJson_ThrowsInvalidDataException_NamingTheFile()
    {
        string dir = NewDir();
        try
        {
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "broken.json");
            File.WriteAllText(path, "[ { \"id\": ");

            var ex = Assert.Throws<InvalidDataException>(() => new FileCatalogStore(path).List());

            Assert.Contains("broken.json", ex.Message);
        }
        finally { Cleanup(dir); }
    }

    [Fact]
    public void List_WhenFileAndDirectoryAreMissing_ReturnsEmpty()
    {
        string path = Path.Combine(NewDir(), "missing", "items.json");

        Assert.Empty(new FileCatalogStore(path).List());
    }

    [Fact]
    public void WriteAtomic_ReplacesWholeCollection_AndCatalogJsonReadsItBack()
    {
        string dir = NewDir();
        try
        {
            string path = Path.Combine(dir, "export.json");
            CatalogJson.WriteAtomic(path, TestData.Items());
            CatalogJson.WriteAtomic(path, TestData.Items().Take(2));

            var dtos = CatalogJson.Deserialize(File.ReadAllText(path));

            Assert.Equal(2, dtos.Count);
            Assert.Equal("T-001", dtos[0].Id);
            Assert.Empty(Directory.GetFiles(dir, "*.tmp"));
        }
        finally { Cleanup(dir); }
    }
}
