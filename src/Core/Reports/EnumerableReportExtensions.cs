namespace Core.Reports;

public static class EnumerableReportExtensions
{
    // Узагальнений top-N за цілим ключем. За однакових значень OrderByDescending зберігає порядок джерела.
    public static IReadOnlyList<T> TopBy<T>(this IEnumerable<T> source, Func<T, int> keySelector, int count)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count), count, "Кількість позицій не може бути від'ємною.");

        return source
            .OrderByDescending(keySelector)
            .Take(count)
            .ToList()
            .AsReadOnly();
    }
}
