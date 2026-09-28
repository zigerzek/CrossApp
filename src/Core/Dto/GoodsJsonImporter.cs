using System.Text;
using System.Text.Json;
using Core.Dto;

namespace Core.Import;

public static class GoodsJsonImporter
{
    public static ImportResult<GoodsDto> Load(string path)
    {
        string json = File.ReadAllText(path, Encoding.UTF8);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var items = JsonSerializer.Deserialize<List<GoodsDto>>(json, options) ?? [];
        return new ImportResult<GoodsDto>(items, []);
    }
}