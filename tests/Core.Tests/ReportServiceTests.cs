using Core.Domain;
using Core.Reports;
using Xunit;

namespace Core.Tests;

public sealed class ReportServiceTests
{
    [Theory]
    [InlineData(10, 2)]
    [InlineData(1, 0)]
    public void LowStock_CountsOnlyBelowThreshold(int threshold, int expected) =>
        Assert.Equal(expected, ReportService.LowStock(TestData.Items(), threshold).Count);

    [Fact]
    public void TopByQuantity_IsDeterministic() =>
        Assert.Equal(new[] { "SKU-BIG", "SKU-MID" },
            ReportService.TopByQuantity(TestData.Items(), 2).Select(l => l.Sku));

    [Fact]
    public void Totals_CalculatesAggregatesCorrectly()
    {
        var totals = ReportService.Totals(TestData.Items());
        Assert.Equal(4, totals.Items);
        Assert.Equal(157, totals.TotalQuantity);
        Assert.Equal(39.25, totals.AverageQuantity);
        Assert.Equal(100, totals.MaxQuantity);
    }

    [Fact]
    public void Totals_HandlesEmptySequenceWithoutException()
    {
        var totals = ReportService.Totals(Array.Empty<Goods>());
        Assert.Equal(0, totals.Items);
        Assert.Equal(0, totals.TotalQuantity);
        Assert.Equal(0d, totals.AverageQuantity);
        Assert.Equal(0, totals.MaxQuantity);
    }
}
