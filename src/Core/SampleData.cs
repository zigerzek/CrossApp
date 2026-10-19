using Core.Domain;

namespace Core;

public static class SampleData
{
    // Кожен виклик створює нові сутності: запуски в пам'яті незалежні.
    public static IReadOnlyList<Goods> Goods() => new List<Goods>
    {
        Domain.Goods.Create("G-001", "SKU-001", "Цемент М400 25кг", "шт", 100),
        Domain.Goods.Create("G-002", "SKU-002", "Пісок", "т", 18),
        Domain.Goods.Create("G-003", "SKU-003", "Цегла", "шт", 4200),
        Domain.Goods.Create("G-004", "SKU-004", "Фарба біла", "л", 80),
        Domain.Goods.Create("G-005", "SKU-005", "Шпаклівка", "кг", 250),
        Domain.Goods.Create("G-006", "SKU-006", "Кабель UTP cat6", "м", 350),
        Domain.Goods.Create("G-007", "SKU-007", "Саморізи", "уп", 60),
        Domain.Goods.Create("G-008", "SKU-008", "Гіпсокартон", "лист", 120),
        Domain.Goods.Create("G-009", "SKU-009", "Профіль металевий", "шт", 160),
        Domain.Goods.Create("G-010", "SKU-010", "Піна монтажна", "шт", 40),
        Domain.Goods.Create("G-011", "SKU-011", "Ґрунтовка", "л", 75),
        Domain.Goods.Create("G-012", "SKU-012", "Клей для плитки", "кг", 500),
        Domain.Goods.Create("G-013", "SKU-013", "Плитка керамічна", "м2", 90),
        Domain.Goods.Create("G-014", "SKU-014", "Утеплювач", "м2", 200),
        Domain.Goods.Create("G-015", "SKU-015", "Ламінат", "м2", 65),
        Domain.Goods.Create("G-016", "SKU-016", "Плінтус", "м", 180),
        Domain.Goods.Create("G-017", "SKU-017", "Рукавиці", "пара", 30),
        Domain.Goods.Create("G-018", "SKU-018", "Захисні окуляри", "шт", 0)
    }.AsReadOnly();
}
