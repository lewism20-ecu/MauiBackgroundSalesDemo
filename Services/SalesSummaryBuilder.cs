using System.Globalization;
using MauiBackgroundSalesDemo.Models;

namespace MauiBackgroundSalesDemo.Services;

public static class SalesSummaryBuilder
{
    private static readonly string[] Electronics =
        ["Laptop", "Phone", "Tablet", "Monitor", "Keyboard", "Mouse", "Headphones"];
    private static readonly string[] OfficeFurniture = ["Desk", "Chair", "Lamp", "Shelf"];
    private static readonly string[] Apparel = ["Shirt", "Jacket", "Sneakers", "Hat"];

    public static SalesSummarySnapshot Build(IReadOnlyList<ProductSaleTransaction> sales)
    {
        ArgumentNullException.ThrowIfNull(sales);

        var daily = sales
            .GroupBy(sale => sale.Timestamp.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new DailySalesSummary(group.Key, group.Sum(sale => sale.Total), group.LongCount()))
            .ToArray();

        var products = sales
            .GroupBy(sale => sale.Product, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new ProductSalesSummary(
                group.Key, group.Sum(sale => sale.Total), group.Sum(sale => (long)sale.Quantity)))
            .ToArray();

        var categories = sales
            .GroupBy(sale => GetCategory(sale.Product), StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new CategorySalesSummary(
                group.Key, group.Sum(sale => sale.Total), group.Sum(sale => (long)sale.Quantity)))
            .ToArray();

        return new SalesSummarySnapshot(daily, products, categories);
    }

    public static string GetCategory(string product)
    {
        ArgumentNullException.ThrowIfNull(product);
        var name = product.Trim();

        if (Electronics.Any(word => name.Contains(word, StringComparison.OrdinalIgnoreCase)))
            return "Electronics";
        if (OfficeFurniture.Any(word => name.Contains(word, StringComparison.OrdinalIgnoreCase)))
            return "Office Furniture";
        if (Apparel.Any(word => name.Contains(word, StringComparison.OrdinalIgnoreCase)))
            return "Apparel";
        return "General";
    }
}
