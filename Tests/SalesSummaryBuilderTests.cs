using MauiBackgroundSalesDemo.Models;
using MauiBackgroundSalesDemo.Services;

namespace SalesDemo.Tests;

public class SalesSummaryBuilderTests
{
    [Theory]
    [InlineData("  laptop desk  ", "Electronics")]
    [InlineData("ERGONOMIC chair", "Office Furniture")]
    [InlineData("blue t-shirt", "Apparel")]
    [InlineData("notebook", "General")]
    public void CategoryMatchingIsTrimmedCaseInsensitiveAndOrdered(string product, string expected)
    {
        Assert.Equal(expected, SalesSummaryBuilder.GetCategory(product));
    }

    [Fact]
    public void SummariesUseDecimalRevenueAndGroupByDateProductAndCategory()
    {
        ProductSaleTransaction[] sales =
        [
            Sale(new DateTime(2026, 11, 1, 9, 0, 0), "Laptop", 2, 0.10m),
            Sale(new DateTime(2026, 11, 1, 10, 0, 0), "Laptop", 1, 0.20m),
            Sale(new DateTime(2026, 11, 2, 9, 0, 0), "Hat", 3, 0.30m)
        ];

        var result = SalesSummaryBuilder.Build(sales);

        Assert.Equal(new DailySalesSummary("2026-11-01", 0.40m, 2), result.Daily[0]);
        Assert.Equal(new DailySalesSummary("2026-11-02", 0.90m, 1), result.Daily[1]);
        Assert.Contains(new ProductSalesSummary("Laptop", 0.40m, 3), result.Products);
        Assert.Contains(new CategorySalesSummary("Electronics", 0.40m, 3), result.Categories);
        Assert.Contains(new CategorySalesSummary("Apparel", 0.90m, 3), result.Categories);
    }

    [Fact]
    public void EmptyInputProducesEmptySummaries()
    {
        var result = SalesSummaryBuilder.Build([]);

        Assert.Empty(result.Daily);
        Assert.Empty(result.Products);
        Assert.Empty(result.Categories);
    }

    private static ProductSaleTransaction Sale(DateTime timestamp, string product, int quantity, decimal price) =>
        new() { Timestamp = timestamp, Product = product, Quantity = quantity, Price = price };
}
