namespace MauiBackgroundSalesDemo.Models;

public sealed record DailySalesSummary(string DateKey, decimal TotalRevenue, long TransactionCount);

public sealed record ProductSalesSummary(string Product, decimal TotalRevenue, long UnitsSold);

public sealed record CategorySalesSummary(string Category, decimal TotalRevenue, long UnitsSold);

public sealed record SalesSummarySnapshot(
    IReadOnlyList<DailySalesSummary> Daily,
    IReadOnlyList<ProductSalesSummary> Products,
    IReadOnlyList<CategorySalesSummary> Categories);
