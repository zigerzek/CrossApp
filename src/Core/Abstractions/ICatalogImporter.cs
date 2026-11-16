using Core.Domain;

namespace Core.Abstractions;

public interface ICatalogImporter
{
    Task<IReadOnlyList<Goods>> LoadAsync(string path, CancellationToken ct = default);

    // Додаткове завдання 1: потоковий імпорт через IAsyncEnumerable
    IAsyncEnumerable<Goods> ReadStreamAsync(string path, CancellationToken ct = default);
}
