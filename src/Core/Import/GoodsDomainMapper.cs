using Core.Domain;
using Core.Dto;

namespace Core.Import;

public static class GoodsDomainMapper
{
    public static ImportResult<Goods> Map(ImportResult<GoodsDto> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(source.Items);
        ArgumentNullException.ThrowIfNull(source.Errors);
        if (source.SourceLineNumbers is { } numbers &&
            (numbers.Count != source.Items.Count || numbers.Any(number => number <= 0)))
            throw new ArgumentException("Номери рядків мають відповідати записам і бути додатними",
                nameof(source));

        var items = new List<Goods>();
        var errors = new List<string>(source.Errors);
        var sourceLineNumbers = new List<int>();

        for (int i = 0; i < source.Items.Count; i++)
        {
            try
            {
                items.Add(Goods.FromDto(source.Items[i]));
                if (source.SourceLineNumbers is { } lines)
                    sourceLineNumbers.Add(lines[i]);
            }
            catch (ArgumentException ex)
            {
                string location = source.SourceLineNumbers is { } lines
                    ? $"рядок {lines[i]}"
                    : $"запис {i + 1}";
                errors.Add($"{location}: інваріант домену — {ex.Message}");
            }
        }

        return new ImportResult<Goods>(items.AsReadOnly(), errors.AsReadOnly(),
            source.SourceLineNumbers is null ? null : sourceLineNumbers.AsReadOnly());
    }
}
