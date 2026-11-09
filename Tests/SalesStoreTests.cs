using LiteDB;
using MauiBackgroundSalesDemo.Models;
using MauiBackgroundSalesDemo.Services;
using Microsoft.Data.Sqlite;

namespace SalesDemo.Tests;

public class SalesStoreTests
{
    [Fact]
    public async Task SeedIsInsertedOnceAndSalesPersistAcrossStoreInstances()
    {
        using var database = new TestDatabase();

        await database.Transactions.InitializeAndSeedAsync();
        await database.Transactions.InitializeAndSeedAsync();
        var seeded = await database.Transactions.GetAllAsync();
        Assert.Equal(8, seeded.Count);
        Assert.Equal(8, seeded.Select(sale => sale.Id).Distinct().Count());

        await database.Transactions.InsertAsync(Sale("Laptop", 2, 10m));
        var reopened = new LiteDbSalesStore(database.TransactionsPath);
        await reopened.InitializeAndSeedAsync();
        var all = await reopened.GetAllAsync();
        Assert.Equal(9, all.Count);
        Assert.Contains(all, sale => sale.Product == "Laptop" && sale.Quantity == 2 && sale.Price == 10m);

        using var raw = new LiteDatabase(database.TransactionsPath);
        var collection = raw.GetCollection<ProductSaleTransaction>("sales");
        Assert.False(collection.EnsureIndex(sale => sale.Timestamp));
        Assert.False(collection.EnsureIndex(sale => sale.Product));
    }

    [Fact]
    public async Task FailedReplacementRollsBackAllThreeTables()
    {
        using var database = new TestDatabase();
        await database.Coordinator.EnsureInitializedAsync();
        var before = await database.Aggregates.ReadAsync();
        var invalid = new SalesSummarySnapshot(
            [new DailySalesSummary("2026-11-01", 100m, 1)],
            [new ProductSalesSummary("Duplicate", 50m, 1), new ProductSalesSummary("Duplicate", 50m, 1)],
            [new CategorySalesSummary("General", 100m, 2)]);

        await Assert.ThrowsAsync<SqliteException>(() => database.Aggregates.ReplaceAsync(invalid));

        var after = await database.Aggregates.ReadAsync();
        Assert.Equal(before.Daily, after.Daily);
        Assert.Equal(before.Products, after.Products);
        Assert.Equal(before.Categories, after.Categories);
    }

    [Fact]
    public async Task ConcurrentInsertsAndRefreshesLeaveTheLatestSnapshot()
    {
        using var database = new TestDatabase();
        await database.Coordinator.EnsureInitializedAsync();
        var tasks = Enumerable.Range(1, 12).Select(async index =>
        {
            await database.Transactions.InsertAsync(Sale("Laptop", index, 1m));
            await database.Coordinator.RefreshAsync();
        });

        await Task.WhenAll(tasks);

        var sales = await database.Transactions.GetAllAsync();
        var expected = SalesSummaryBuilder.Build(sales);
        var actual = await database.Aggregates.ReadAsync();
        Assert.Equal(20, sales.Count);
        Assert.Equal(expected.Daily.Sum(row => row.TransactionCount), actual.Daily.Sum(row => row.TransactionCount));
        Assert.Equal(
            expected.Products.Single(row => row.Product == "Laptop").UnitsSold,
            actual.Products.Single(row => row.Product == "Laptop").UnitsSold);
        Assert.Equal(
            decimal.Round(expected.Products.Single(row => row.Product == "Laptop").TotalRevenue, 2),
            decimal.Round(actual.Products.Single(row => row.Product == "Laptop").TotalRevenue, 2));
    }

    [Fact]
    public async Task CancellationIsForwardedBeforeInitialization()
    {
        using var database = new TestDatabase();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => database.Coordinator.EnsureInitializedAsync(cancellation.Token));
        Assert.False(File.Exists(database.TransactionsPath));
        Assert.False(File.Exists(database.AggregatesPath));
    }

    [Fact]
    public async Task MissingAggregateDatabaseIsRecreatedOnTheNextRefresh()
    {
        using var database = new TestDatabase();
        await database.Coordinator.EnsureInitializedAsync();
        await database.Transactions.InsertAsync(Sale("Laptop", 2, 25m));
        File.Delete(database.AggregatesPath);

        await database.Coordinator.RefreshAsync();

        var restored = await database.Aggregates.ReadAsync();
        Assert.Equal(9, restored.Daily.Sum(row => row.TransactionCount));
        Assert.Equal(9, (await database.Transactions.GetAllAsync()).Count);
    }

    [Fact]
    public async Task MissingTransactionDatabaseDoesNotSilentlyBecomeAnEmptySystemOfRecord()
    {
        using var database = new TestDatabase();
        await database.Coordinator.EnsureInitializedAsync();
        File.Move(database.TransactionsPath, database.TransactionsPath + ".backup");

        await Assert.ThrowsAsync<FileNotFoundException>(() => database.Coordinator.RefreshAsync());

        Assert.False(File.Exists(database.TransactionsPath));
        Assert.Equal(8, (await database.Aggregates.ReadAsync()).Daily.Sum(row => row.TransactionCount));
    }

    [Fact]
    public async Task InitializationCanRetryWithoutDuplicatingSeedRecords()
    {
        using var database = new TestDatabase();
        Directory.CreateDirectory(database.AggregatesPath);

        await Assert.ThrowsAsync<SqliteException>(() => database.Coordinator.EnsureInitializedAsync());
        Directory.Delete(database.AggregatesPath);
        await database.Coordinator.EnsureInitializedAsync();

        Assert.Equal(8, (await database.Transactions.GetAllAsync()).Count);
        Assert.Equal(8, (await database.Aggregates.ReadAsync()).Daily.Sum(row => row.TransactionCount));
    }

    private static ProductSaleTransaction Sale(string product, int quantity, decimal price) =>
        new() { Timestamp = DateTime.Now, Product = product, Quantity = quantity, Price = price };
}
