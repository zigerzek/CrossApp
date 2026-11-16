using Core.Domain;
using Core.Services;
using Core.Storage;

namespace Core.Tests;

/// <summary>Експорт → імпорт власного файлу (лаб. 9) і збереження суворого правила лаб. 8.</summary>
public sealed class CollectionRoundTripTests
{
    private static string NewTempFile() => Path.Combine(Path.GetTempPath(), $"crossapp_roundtrip_{Guid.NewGuid():N}.json");

    private static IReadOnlyList<Goods> WithZeroStock() =>
    [
        Goods.Create("R-1", "SKU-R1", "Цемент", "мішок", 40),
        Goods.Create("R-2", "SKU-R2", "Окуляри", "шт", 0)
    ];

    [Fact]
    public async Task ExportedCollection_WithZeroStock_ImportsBack_WhenCollectionImporterIsUsed()
    {
        string path = NewTempFile();
        try
        {
            CatalogJson.WriteAtomic(path, WithZeroStock());
            var store = new InMemoryCatalogStore();

            int added = await new CatalogService(store)
                .ImportAsync(new JsonCatalogImporter(allowZeroQuantity: true), path);

            Assert.Equal(2, added);
            Assert.Equal(0, store.GetById("R-2")!.Quantity);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task DefaultImporter_StillRejectsZeroQuantity_AsLab8Requires()
    {
        string path = NewTempFile();
        try
        {
            CatalogJson.WriteAtomic(path, WithZeroStock());

            var ex = await Assert.ThrowsAsync<DomainException>(
                () => new CatalogService(new InMemoryCatalogStore()).ImportAsync(new JsonCatalogImporter(), path));

            Assert.Contains("Запис 2", ex.Message);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
