using Core.Domain;

namespace Core;

public static class SampleData
{
    // 18 незалежних сутностей та рівно 4 групи одиниць вимірювання для report-groups.
    public static IReadOnlyList<Goods> Goods() => new List<Goods>
    {
        Domain.Goods.Create("G-001", "SKU-001", "Цемент М400 25кг", "шт", 100),
        Domain.Goods.Create("G-002", "SKU-002", "Пісок", "кг", 18),
        Domain.Goods.Create("G-003", "SKU-003", "Цегла", "шт", 4200),
        Domain.Goods.Create("G-004", "SKU-004", "Фарба біла", "л", 80),
        Domain.Goods.Create("G-005", "SKU-005", "Шпаклівка", "кг", 250),
        Domain.Goods.Create("G-006", "SKU-006", "Кабель UTP cat6", "м", 350),
        Domain.Goods.Create("G-007", "SKU-007", "Саморізи", "шт", 60),
        Domain.Goods.Create("G-008", "SKU-008", "Гіпсокартон", "шт", 120),
        Domain.Goods.Create("G-009", "SKU-009", "Профіль металевий", "м", 160),
        Domain.Goods.Create("G-010", "SKU-010", "Піна монтажна", "шт", 40),
        Domain.Goods.Create("G-011", "SKU-011", "Ґрунтовка", "л", 75),
        Domain.Goods.Create("G-012", "SKU-012", "Клей для плитки", "кг", 500),
        Domain.Goods.Create("G-013", "SKU-013", "Плитка керамічна", "шт", 90),
        Domain.Goods.Create("G-014", "SKU-014", "Утеплювач", "шт", 200),
        Domain.Goods.Create("G-015", "SKU-015", "Ламінат", "шт", 65),
        Domain.Goods.Create("G-016", "SKU-016", "Плінтус", "м", 180),
        Domain.Goods.Create("G-017", "SKU-017", "Рукавиці", "шт", 30),
        Domain.Goods.Create("G-018", "SKU-018", "Захисні окуляри", "шт", 0)
    }.AsReadOnly();

    // Пов'язані сутності для додаткового Join-звіту лабораторної 7.
    public static IReadOnlyList<Domain.Batch> Batches() => new List<Domain.Batch>
    {
        Domain.Batch.Create("B-001", "G-003", 1_000, new DateOnly(2026, 10, 1)),
        Domain.Batch.Create("B-002", "G-006", 200, new DateOnly(2026, 10, 5)),
        Domain.Batch.Create("B-003", "G-012", 150, new DateOnly(2026, 10, 10)),
        Domain.Batch.Create("B-004", "G-018", 5, new DateOnly(2026, 10, 15))
    }.AsReadOnly();
}
