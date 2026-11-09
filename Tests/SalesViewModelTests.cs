using System.Globalization;
using MauiBackgroundSalesDemo.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;

namespace SalesDemo.Tests;

public class SalesViewModelTests
{
    [Fact]
    public void ProductSelectionUpdatesTheSuggestedPriceAndRaisesNotifications()
    {
        using var database = new TestDatabase();
        var viewModel = CreateViewModel(database);
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        Assert.Null(viewModel.SelectedProduct);
        Assert.Equal("1", viewModel.QuantityText);
        Assert.Empty(viewModel.UnitPriceText);
        Assert.Equal("Choose a product and enter the sale details.", viewModel.StatusMessage);

        viewModel.SelectedProduct = "Laptop";

        Assert.Equal(999.99m.ToString("0.00", CultureInfo.CurrentCulture), viewModel.UnitPriceText);
        Assert.Contains(nameof(viewModel.SelectedProduct), changed);
        Assert.Contains(nameof(viewModel.UnitPriceText), changed);
    }

    [Theory]
    [InlineData(null, "1", "25", "Select a product")]
    [InlineData("Laptop", "0", "25", "positive whole number")]
    [InlineData("Laptop", "invalid", "25", "positive whole number")]
    [InlineData("Laptop", "1", "0", "Unit price must be")]
    [InlineData("Laptop", "1", "-1", "Unit price must be")]
    [InlineData("Laptop", "1", "invalid", "Unit price must be")]
    public async Task InvalidCurrentInputDoesNotInsertASale(
        string? product, string quantity, string price, string expectedMessage)
    {
        using var database = new TestDatabase();
        await database.Coordinator.EnsureInitializedAsync();
        var viewModel = CreateViewModel(database);
        viewModel.SelectedProduct = product;
        viewModel.QuantityText = quantity;
        viewModel.UnitPriceText = price;

        await viewModel.AddTransactionCommand.ExecuteAsync(null);

        Assert.Contains(expectedMessage, viewModel.StatusMessage);
        Assert.Equal(8, (await database.Transactions.GetAllAsync()).Count);
    }

    [Fact]
    public async Task ValidSaleImmediatelyUpdatesTheSqliteSnapshot()
    {
        using var database = new TestDatabase();
        await database.Coordinator.EnsureInitializedAsync();
        var before = await database.Aggregates.ReadAsync();
        var viewModel = CreateViewModel(database);
        viewModel.SelectedProduct = "Laptop";
        viewModel.QuantityText = "2";
        viewModel.UnitPriceText = "25";

        await viewModel.AddTransactionCommand.ExecuteAsync(null);

        var after = await database.Aggregates.ReadAsync();
        Assert.Contains("Dashboard updated", viewModel.StatusMessage);
        Assert.Equal(
            before.Products.Single(row => row.Product == "Laptop").TotalRevenue + 50m,
            after.Products.Single(row => row.Product == "Laptop").TotalRevenue);
        Assert.Equal(9, (await database.Transactions.GetAllAsync()).Count);
    }

    [Fact]
    public async Task SavedSaleIsReportedSeparatelyWhenRefreshFailsAndCanCatchUpLater()
    {
        using var database = new TestDatabase();
        await database.Coordinator.EnsureInitializedAsync();
        var before = await database.Aggregates.ReadAsync();
        var backup = database.AggregatesPath + ".backup";
        File.Move(database.AggregatesPath, backup);
        Directory.CreateDirectory(database.AggregatesPath);
        var viewModel = CreateViewModel(database);
        viewModel.SelectedProduct = "Laptop";
        viewModel.QuantityText = "2";
        viewModel.UnitPriceText = "25";

        await viewModel.AddTransactionCommand.ExecuteAsync(null);

        Assert.Contains("Saved Laptop", viewModel.StatusMessage);
        Assert.Contains("refresh pending", viewModel.StatusMessage);
        Assert.Equal(9, (await database.Transactions.GetAllAsync()).Count);
        Directory.Delete(database.AggregatesPath);
        File.Move(backup, database.AggregatesPath);
        await database.Coordinator.RefreshAsync();
        var recovered = await database.Aggregates.ReadAsync();
        Assert.Equal(
            before.Products.Single(row => row.Product == "Laptop").TotalRevenue + 50m,
            recovered.Products.Single(row => row.Product == "Laptop").TotalRevenue);
    }

    [Fact]
    public async Task SaleCanBeSavedWhenSqliteIsUnavailableDuringInitialStartup()
    {
        using var database = new TestDatabase();
        Directory.CreateDirectory(database.AggregatesPath);
        var viewModel = CreateViewModel(database);
        viewModel.SelectedProduct = "Laptop";
        viewModel.QuantityText = "2";
        viewModel.UnitPriceText = "25";

        await viewModel.AddTransactionCommand.ExecuteAsync(null);

        Assert.Contains("Saved Laptop", viewModel.StatusMessage);
        Assert.Contains("refresh pending", viewModel.StatusMessage);
        Assert.Equal(9, (await database.Transactions.GetAllAsync()).Count);

        Directory.Delete(database.AggregatesPath);
        await database.Coordinator.RefreshAsync();
        Assert.Equal(9, (await database.Aggregates.ReadAsync()).Daily.Sum(row => row.TransactionCount));
    }

    [Fact]
    public async Task MissingTransactionDatabaseIsNotRecreatedWhenSaving()
    {
        using var database = new TestDatabase();
        await database.Coordinator.EnsureInitializedAsync();
        File.Move(database.TransactionsPath, database.TransactionsPath + ".backup");
        var viewModel = CreateViewModel(database);
        viewModel.SelectedProduct = "Laptop";
        viewModel.QuantityText = "1";
        viewModel.UnitPriceText = "25";

        await viewModel.AddTransactionCommand.ExecuteAsync(null);

        Assert.Contains("not saved", viewModel.StatusMessage);
        Assert.False(File.Exists(database.TransactionsPath));
        Assert.True(File.Exists(database.TransactionsPath + ".backup"));
    }

    private static SalesViewModel CreateViewModel(TestDatabase database) =>
        new(database.Transactions, database.Coordinator, NullLogger<SalesViewModel>.Instance);
}
