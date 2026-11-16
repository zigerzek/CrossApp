using Core.Domain;

namespace Core.Tests;

public static class TestData
{
    public static IReadOnlyList<Goods> Items() => new List<Goods>
    {
        Goods.Create("T-001", "SKU-BIG", "Великий товар", "шт", 100),
        Goods.Create("T-002", "SKU-MID", "Середній товар", "шт", 50),
        Goods.Create("T-003", "SKU-LOW1", "Малий товар 1", "шт", 5),
        Goods.Create("T-004", "SKU-LOW2", "Малий товар 2", "шт", 2)
    }.AsReadOnly();
}
