using Core.Abstractions;
using Core.Domain;
using Core.Services;
using Core.Storage;
using Xunit;

namespace Core.Tests;

public sealed class ImportTests
{
    [Fact]
    public async Task ImportAsync_WhenAlreadyCancelled_Throws()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var service = new CatalogService(new InMemoryCatalogStore());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.ImportAsync(new JsonCatalogImporter(), "any.json", cts.Token));
    }

    [Fact]
    public async Task ImportAsync_WithCorruptedFile_ThrowsDomainException_AndCleansUpTempFile()
    {
        string tempPath = Path.Combine(Path.GetTempPath(), $"invalid_catalog_{Guid.NewGuid():N}.json");
        try
        {
            string badJson = """
            [
                { "id": "G-999", "sku": "SKU-999", "name": "Некоректний", "unit": "шт", "quantity": 0 }
            ]
            """;
            await File.WriteAllTextAsync(tempPath, badJson);

            var service = new CatalogService(new InMemoryCatalogStore());
            var ex = await Assert.ThrowsAsync<DomainException>(
                () => service.ImportAsync(new JsonCatalogImporter(), tempPath));

            Assert.Contains("кількість не може бути 0", ex.Message);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task ImportAsync_WithMissingList_ThrowsDomainException()
    {
        string tempPath = Path.Combine(Path.GetTempPath(), $"empty_obj_{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(tempPath, "null");

            var service = new CatalogService(new InMemoryCatalogStore());
            var ex = await Assert.ThrowsAsync<DomainException>(
                () => service.ImportAsync(new JsonCatalogImporter(), tempPath));

            Assert.Contains("не містить списку", ex.Message);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task ImportAsync_WithValidFile_ImportsSuccessfully_AndIsIdempotent()
    {
        string tempPath = Path.Combine(Path.GetTempPath(), $"valid_catalog_{Guid.NewGuid():N}.json");
        try
        {
            string validJson = """
            [
                { "id": "G-101", "sku": "SKU-101", "name": "Товар 1", "unit": "шт", "quantity": 10 },
                { "id": "G-102", "sku": "SKU-102", "name": "Товар 2", "unit": "кг", "quantity": 25 }
            ]
            """;
            await File.WriteAllTextAsync(tempPath, validJson);

            var store = new InMemoryCatalogStore();
            var service = new CatalogService(store);
            int addedFirst = await service.ImportAsync(new JsonCatalogImporter(), tempPath);
            Assert.Equal(2, addedFirst);
            Assert.Equal(2, store.List().Count);

            // Повторний імпорт не дублює записи (ідемпотентність)
            int addedSecond = await service.ImportAsync(new JsonCatalogImporter(), tempPath);
            Assert.Equal(0, addedSecond);
            Assert.Equal(2, store.List().Count);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task ImportStreamAsync_StreamsItems_UsingAsyncEnumerable()
    {
        string tempPath = Path.Combine(Path.GetTempPath(), $"stream_catalog_{Guid.NewGuid():N}.json");
        try
        {
            string validJson = """
            [
                { "id": "S-01", "sku": "SKU-S1", "name": "Потоковий 1", "unit": "шт", "quantity": 15 },
                { "id": "S-02", "sku": "SKU-S2", "name": "Потоковий 2", "unit": "м", "quantity": 30 }
            ]
            """;
            await File.WriteAllTextAsync(tempPath, validJson);

            var store = new InMemoryCatalogStore();
            var service = new CatalogService(store);
            var progressReports = new List<int>();
            var progress = new SynchronousProgress<int>(progressReports.Add);

            int added = await service.ImportStreamAsync(new JsonCatalogImporter(), tempPath, progress);
            Assert.Equal(2, added);
            Assert.Equal(2, store.List().Count);
            Assert.Equal(new[] { 1, 2 }, progressReports);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task Import_WithLinkedTokenSource_ProvesTimeoutTriggered()
    {
        using var userCts = new CancellationTokenSource();
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(userCts.Token, timeoutCts.Token);

        await Task.Delay(60);

        Assert.False(userCts.IsCancellationRequested, "Користувацький токен не повинен бути скасованим.");
        Assert.True(timeoutCts.IsCancellationRequested, "Токен таймауту має бути скасованим.");
        Assert.True(linkedCts.IsCancellationRequested, "Зв'язаний токен повинен бути скасованим саме через таймаут.");

        var service = new CatalogService(new InMemoryCatalogStore());
        var ex = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.ImportAsync(new JsonCatalogImporter(), "any.json", linkedCts.Token));

        Assert.Equal(linkedCts.Token, ex.CancellationToken);
    }

    [Fact]
    public async Task ImportAsync_ReportsProgressCorrectly()
    {
        string tempPath = Path.Combine(Path.GetTempPath(), $"progress_catalog_{Guid.NewGuid():N}.json");
        try
        {
            string validJson = """
            [
                { "id": "P-01", "sku": "SKU-P1", "name": "Прогрес 1", "unit": "шт", "quantity": 10 },
                { "id": "P-02", "sku": "SKU-P2", "name": "Прогрес 2", "unit": "шт", "quantity": 20 }
            ]
            """;
            await File.WriteAllTextAsync(tempPath, validJson);

            var store = new InMemoryCatalogStore();
            var service = new CatalogService(store);
            var reports = new List<int>();
            var progress = new SynchronousProgress<int>(reports.Add);

            int added = await service.ImportAsync(new JsonCatalogImporter(), tempPath, progress);
            Assert.Equal(2, added);
            Assert.Contains(50, reports);
            Assert.Contains(100, reports);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    private sealed class SynchronousProgress<T>(Action<T> action) : IProgress<T>
    {
        public void Report(T value) => action(value);
    }
}
