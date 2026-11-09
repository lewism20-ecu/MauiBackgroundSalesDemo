using MauiBackgroundSalesDemo.Services;

namespace SalesDemo.Tests;

public class SalesSeedDataTests
{
    [Fact]
    public void SeedRecordsMatchTheSuppliedAssignmentData()
    {
        var now = new DateTime(2026, 11, 9, 12, 30, 0, DateTimeKind.Local);
        (DateTime Timestamp, string Product, int Quantity, decimal Price)[] expected =
        [
            (now.AddDays(-5), "Laptop", 2, 999.99m),
            (now.AddDays(-4), "Phone", 4, 699.00m),
            (now.AddDays(-3), "Monitor", 1, 329.50m),
            (now.AddDays(-2), "Keyboard", 3, 79.99m),
            (now.AddDays(-1), "Headphones", 5, 129.00m),
            (now, "Laptop", 1, 1249.00m),
            (now, "Desk Chair", 2, 245.00m),
            (now.AddHours(-6), "Phone", 3, 719.00m)
        ];

        var actual = SalesSeedData.Create(now)
            .Select(sale => (sale.Timestamp, sale.Product, sale.Quantity, sale.Price))
            .ToArray();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task SeededSummariesMatchTheSuppliedAssignmentData()
    {
        using var database = new TestDatabase();
        await database.Coordinator.EnsureInitializedAsync();

        var summary = await database.Aggregates.ReadAsync();

        Assert.Equal(8, summary.Daily.Sum(row => row.TransactionCount));
        Assert.Equal(9906.45m, decimal.Round(summary.Daily.Sum(row => row.TotalRevenue), 2));
        var phone = Assert.Single(summary.Products, row => row.Product == "Phone");
        Assert.Equal(4953.00m, decimal.Round(phone.TotalRevenue, 2));
        Assert.Equal(7, phone.UnitsSold);
        var electronics = Assert.Single(summary.Categories, row => row.Category == "Electronics");
        Assert.Equal(9416.45m, decimal.Round(electronics.TotalRevenue, 2));
        Assert.Equal(19, electronics.UnitsSold);
        var office = Assert.Single(summary.Categories, row => row.Category == "Office Furniture");
        Assert.Equal(490.00m, decimal.Round(office.TotalRevenue, 2));
        Assert.Equal(2, office.UnitsSold);
    }
}
