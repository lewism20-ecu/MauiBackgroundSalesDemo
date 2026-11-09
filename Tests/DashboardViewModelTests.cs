using System.Globalization;
using MauiBackgroundSalesDemo.Models;
using MauiBackgroundSalesDemo.ViewModels;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace SalesDemo.Tests;

public class DashboardViewModelTests
{
    [Fact]
    public void InitialPropertiesRetainTheirValuesAndRaiseChangeNotifications()
    {
        using var database = new TestDatabase();
        var viewModel = CreateViewModel(database);
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        Assert.Equal(0m.ToString("C2", CultureInfo.CurrentCulture), viewModel.TotalRevenue);
        Assert.Equal("0", viewModel.TransactionCount);
        Assert.Equal(0m.ToString("C2", CultureInfo.CurrentCulture), viewModel.AverageSale);
        Assert.Equal("No sales yet", viewModel.TopProduct);
        Assert.Equal("Loading summaries...", viewModel.StatusMessage);
        Assert.False(viewModel.IsRefreshing);

        viewModel.TransactionCount = "1";

        Assert.Contains(nameof(viewModel.TransactionCount), changed);
    }

    [Fact]
    public async Task RefreshReadsSqliteAndScalesEachNonemptyChart()
    {
        using var database = new TestDatabase();
        await database.Coordinator.EnsureInitializedAsync();
        var viewModel = CreateViewModel(database);

        await viewModel.RefreshAsync();

        Assert.Equal("8", viewModel.TransactionCount);
        var rows = viewModel.ChartRows.ToArray();
        var headings = rows.OfType<SalesChartHeading>().ToArray();
        Assert.Equal(["Daily sales", "Products", "Categories"], headings.Select(heading => heading.Title));
        foreach (var heading in headings)
        {
            var items = rows.Skip(Array.IndexOf(rows, heading) + 1)
                .TakeWhile(row => row is not SalesChartHeading)
                .Cast<SalesChartItem>()
                .ToArray();
            Assert.NotEmpty(items);
            Assert.Equal(1d, items.Max(item => item.Progress));
            Assert.All(items, item => Assert.InRange(item.Progress, 0d, 1d));
        }

        await database.Transactions.InsertAsync(new ProductSaleTransaction
        {
            Timestamp = DateTime.Now,
            Product = "Laptop",
            Quantity = 1,
            Price = 1m
        });
        await database.Coordinator.RefreshAsync();
        await viewModel.RefreshAsync();
        Assert.Equal("9", viewModel.TransactionCount);
    }

    [Fact]
    public async Task EmptySnapshotShowsZeroMetricsWithoutDividingByZero()
    {
        using var database = new TestDatabase();
        await database.Coordinator.EnsureInitializedAsync();
        await database.Aggregates.ReplaceAsync(new SalesSummarySnapshot([], [], []));
        var viewModel = CreateViewModel(database);

        await viewModel.RefreshAsync();

        Assert.Equal("0", viewModel.TransactionCount);
        Assert.Equal(0m.ToString("C2", CultureInfo.CurrentCulture), viewModel.TotalRevenue);
        Assert.Equal(0m.ToString("C2", CultureInfo.CurrentCulture), viewModel.AverageSale);
        Assert.Equal("No sales yet", viewModel.TopProduct);
        Assert.Equal(3, viewModel.ChartRows.Count);
        Assert.All(viewModel.ChartRows, row => Assert.IsType<SalesChartHeading>(row));
    }

    [Fact]
    public async Task OverlappingRefreshRequestsAreQueuedInsteadOfSkipped()
    {
        using var database = new TestDatabase();
        await database.Coordinator.EnsureInitializedAsync();
        using var connection = new SqliteConnection($"Data Source={database.AggregatesPath};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "BEGIN EXCLUSIVE;";
        command.ExecuteNonQuery();
        var viewModel = CreateViewModel(database);

        var first = viewModel.RefreshAsync();
        var second = viewModel.RefreshAsync();

        Assert.False(first.IsCompleted);
        Assert.False(second.IsCompleted);
        command.CommandText = "COMMIT;";
        command.ExecuteNonQuery();
        await Task.WhenAll(first, second);
        Assert.Equal("8", viewModel.TransactionCount);
        Assert.False(viewModel.IsRefreshing);
    }

    private static DashboardViewModel CreateViewModel(TestDatabase database) =>
        new(database.Coordinator, database.Aggregates, NullLogger<DashboardViewModel>.Instance);
}
