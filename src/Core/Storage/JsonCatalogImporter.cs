using System.Runtime.CompilerServices;
using System.Text.Json;
using Core.Abstractions;
using Core.Domain;
using Core.Dto;

namespace Core.Storage;

public sealed class JsonCatalogImporter : ICatalogImporter
{
    private readonly bool _allowZeroQuantity;

    /// <param name="allowZeroQuantity">
    /// <c>false</c> (лаб. 8, імпорт постачань): запис із кількістю 0 — порушення бізнес-правила (<see cref="DomainException"/>).
    /// <c>true</c> (лаб. 9, імпорт усієї колекції): нульовий залишок — допустимий стан складу,
    /// інакше файл, створений експортом, не можна було б імпортувати назад.
    /// </param>
    public JsonCatalogImporter(bool allowZeroQuantity = false) => _allowZeroQuantity = allowZeroQuantity;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<IReadOnlyList<Goods>> LoadAsync(string path, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ct.ThrowIfCancellationRequested();

        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.Read, 4096, useAsync: true);

        var dtos = await JsonSerializer.DeserializeAsync<List<GoodsDto>>(
            stream, JsonOptions, cancellationToken: ct)
            ?? throw new DomainException($"Файл {Path.GetFileName(path)} не містить списку.");

        var result = new List<Goods>(dtos.Count);
        for (var i = 0; i < dtos.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var dto = dtos[i];

            if (!_allowZeroQuantity && dto.Quantity == 0)
                throw new DomainException($"Запис {i + 1}: кількість не може бути 0.");

            try
            {
                result.Add(Goods.FromDto(dto));
            }
            catch (ArgumentException ex)
            {
                throw new DomainException($"Запис {i + 1}: {ex.Message}", ex);
            }
        }

        return result;
    }

    public async IAsyncEnumerable<Goods> ReadStreamAsync(
        string path,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ct.ThrowIfCancellationRequested();

        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.Read, 4096, useAsync: true);

        int index = 0;
        await foreach (var dto in JsonSerializer.DeserializeAsyncEnumerable<GoodsDto>(
            stream, JsonOptions, cancellationToken: ct))
        {
            ct.ThrowIfCancellationRequested();
            if (dto is null) continue;
            index++;

            if (!_allowZeroQuantity && dto.Quantity == 0)
                throw new DomainException($"Запис {index}: кількість не може бути 0.");

            Goods item;
            try
            {
                item = Goods.FromDto(dto);
            }
            catch (ArgumentException ex)
            {
                throw new DomainException($"Запис {index}: {ex.Message}", ex);
            }

            yield return item;
        }
    }
}
