using MauiBackgroundSalesDemo.Models;

namespace MauiBackgroundSalesDemo.Services;

internal static class SalesSeedData
{
    public static IReadOnlyList<ProductSaleTransaction> Create(DateTime now) =>
    [
        Sale(now.AddDays(-5), "Laptop", 2, 999.99m),
        Sale(now.AddDays(-4), "Phone", 4, 699.00m),
        Sale(now.AddDays(-3), "Monitor", 1, 329.50m),
        Sale(now.AddDays(-2), "Keyboard", 3, 79.99m),
        Sale(now.AddDays(-1), "Headphones", 5, 129.00m),
        Sale(now, "Laptop", 1, 1249.00m),
        Sale(now, "Desk Chair", 2, 245.00m),
        Sale(now.AddHours(-6), "Phone", 3, 719.00m)
    ];

    private static ProductSaleTransaction Sale(DateTime timestamp, string product, int quantity, decimal price) =>
        new() { Timestamp = timestamp, Product = product, Quantity = quantity, Price = price };
}
