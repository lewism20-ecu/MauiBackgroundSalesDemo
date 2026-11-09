using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiBackgroundSalesDemo.Models;
using MauiBackgroundSalesDemo.Services;
using Microsoft.Extensions.Logging;

namespace MauiBackgroundSalesDemo.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly SalesAggregationCoordinator _aggregation;
    private readonly SqliteSalesAggregateStore _aggregates;
    private readonly ILogger<DashboardViewModel> _logger;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    [ObservableProperty]
    public partial string TotalRevenue { get; set; }

    [ObservableProperty]
    public partial string TransactionCount { get; set; }

    [ObservableProperty]
    public partial string AverageSale { get; set; }

    [ObservableProperty]
    public partial string TopProduct { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; }

    [ObservableProperty]
    public partial bool IsRefreshing { get; set; }

    public DashboardViewModel(
        SalesAggregationCoordinator aggregation,
        SqliteSalesAggregateStore aggregates,
        ILogger<DashboardViewModel> logger)
    {
        _aggregation = aggregation;
        _aggregates = aggregates;
        _logger = logger;
        TotalRevenue = FormatMoney(0);
        TransactionCount = "0";
        AverageSale = FormatMoney(0);
        TopProduct = "No sales yet";
        StatusMessage = "Loading summaries...";
    }

    public ObservableCollection<SalesChartRow> ChartRows { get; } = [];

    [RelayCommand]
    public async Task RefreshAsync()
    {
        await _refreshGate.WaitAsync();
        try
        {
            IsRefreshing = true;
            await _aggregation.EnsureInitializedAsync();
            var snapshot = await _aggregates.ReadAsync();
            var total = snapshot.Daily.Sum(row => row.TotalRevenue);
            var count = snapshot.Daily.Sum(row => row.TransactionCount);
            TotalRevenue = FormatMoney(total);
            TransactionCount = count.ToString(CultureInfo.CurrentCulture);
            AverageSale = FormatMoney(count == 0 ? 0 : total / count);
            TopProduct = snapshot.Products.FirstOrDefault()?.Product ?? "No sales yet";

            var dailyMax = snapshot.Daily.Select(row => row.TotalRevenue).DefaultIfEmpty(0).Max();
            var productMax = snapshot.Products.Select(row => row.TotalRevenue).DefaultIfEmpty(0).Max();
            var categoryMax = snapshot.Categories.Select(row => row.TotalRevenue).DefaultIfEmpty(0).Max();
            ChartRows.Clear();
            AddChart("Daily sales", snapshot.Daily.Select(row =>
                Chart(row.DateKey, row.TotalRevenue, $"{row.TransactionCount} sales", dailyMax)));
            AddChart("Products", snapshot.Products.Select(row =>
                Chart(row.Product, row.TotalRevenue, $"{row.UnitsSold} units", productMax)));
            AddChart("Categories", snapshot.Categories.Select(row =>
                Chart(row.Category, row.TotalRevenue, $"{row.UnitsSold} units", categoryMax)));
            StatusMessage = $"SQLite summaries checked at {DateTime.Now:t}.";
        }
        catch (Exception exception) when (SalesDataErrors.IsExpected(exception))
        {
            _logger.LogError(exception, "Could not load the dashboard summaries.");
            StatusMessage = "Could not load the dashboard. Select Refresh to try again.";
        }
        finally
        {
            IsRefreshing = false;
            _refreshGate.Release();
        }
    }

    private void AddChart(string title, IEnumerable<SalesChartItem> items)
    {
        ChartRows.Add(new SalesChartHeading(title));
        foreach (var item in items)
        {
            ChartRows.Add(item);
        }
    }

    private static SalesChartItem Chart(string name, decimal revenue, string count, decimal maximum) =>
        new(name, $"{FormatMoney(revenue)} | {count}", maximum > 0 ? (double)(revenue / maximum) : 0);

    private static string FormatMoney(decimal amount) => amount.ToString("C2", CultureInfo.CurrentCulture);
}
