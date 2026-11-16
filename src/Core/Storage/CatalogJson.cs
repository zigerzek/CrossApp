using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Core.Domain;
using Core.Dto;

namespace Core.Storage;

/// <summary>
/// Єдине місце, де описано формат JSON-файлу каталогу (експорт, імпорт, файлове сховище).
/// camelCase, читабельна кирилиця (без \uXXXX), відступи; під час читання регістр імен не важливий.
/// </summary>
public static class CatalogJson
{
    // UnsafeRelaxedJsonEscaping: JSON не вбудовується в HTML, тому кирилицю можна писати як є.
    public static JsonSerializerOptions Options { get; } = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string Serialize(IEnumerable<Goods> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return JsonSerializer.Serialize(items.Select(item => item.ToDto()).ToList(), Options);
    }

    /// <summary>Розбирає JSON-масив. Некоректний JSON → <see cref="JsonException"/>.</summary>
    public static List<GoodsDto> Deserialize(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return JsonSerializer.Deserialize<List<GoodsDto>>(json, Options)
            ?? throw new JsonException("Очікується JSON-масив товарів, а не null.");
    }

    /// <summary>
    /// Атомарний запис усієї колекції: спершу тимчасовий файл у тому самому каталозі,
    /// потім <see cref="File.Move(string, string, bool)"/> з перезаписом. Читач бачить або
    /// старий, або новий файл повністю — ніколи «половину».
    /// </summary>
    public static void WriteAtomic(string path, IEnumerable<Goods> items)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string target = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(target)!;
        Directory.CreateDirectory(directory);

        string temporary = Path.Combine(directory, $".{Path.GetFileName(target)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporary, Serialize(items));
            File.Move(temporary, target, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
