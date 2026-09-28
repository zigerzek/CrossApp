using System.Text;
using Core.Dto;

namespace Core.Import;

public static class GoodsCsvImporter
{
    private const char Separator = ';';

    public static ImportResult<GoodsDto> Load(string path)
    {
        var items = new List<GoodsDto>();
        var errors = new List<string>();
        string[] lines = File.ReadAllLines(path, Encoding.UTF8);

        for (int i = 0; i < lines.Length; i++)
        {
            int number = i + 1;
            string line = lines[i];

            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                continue;
            if (number == 1 && line.StartsWith("id", StringComparison.OrdinalIgnoreCase))
                continue;

            switch (ParseLine(line))
            {
                case ParseOk ok:
                    items.Add(ok.Value);
                    break;
                case ParseFailed failed:
                    errors.Add($"рядок {number}: {failed.Reason}");
                    break;
            }
        }

        return new ImportResult<GoodsDto>(items, errors);
    }

    private static ParseOutcome ParseLine(string line)
    {
        string[] parts = line.Split(Separator, StringSplitOptions.TrimEntries);
        return parts switch
        {
            { Length: < 5 } => new ParseFailed($"очікую 5 колонок, отримав {parts.Length}"),

            [_, "", _, _, _] or [_, _, "", _, _]
                => new ParseFailed("SKU або назва товару порожні"),

            [_, _, _, _, var qty] when !int.TryParse(qty, out int q) || q < 0
                => new ParseFailed($"кількість '{qty}' не є невід'ємним числом"),

            [var id, var sku, var name, var unit, var qty]
                => new ParseOk(new GoodsDto(id, sku, name, unit, int.Parse(qty))),

            _ => new ParseFailed($"забагато колонок: {parts.Length}")
        };
    }
    private abstract record ParseOutcome;
    private sealed record ParseOk(GoodsDto Value) : ParseOutcome;
    private sealed record ParseFailed(string Reason) : ParseOutcome;
}