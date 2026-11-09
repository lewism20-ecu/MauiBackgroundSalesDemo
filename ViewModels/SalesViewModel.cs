using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiBackgroundSalesDemo.Models;
using MauiBackgroundSalesDemo.Services;
using Microsoft.Extensions.Logging;

namespace MauiBackgroundSalesDemo.ViewModels;

public partial class SalesViewModel : ObservableObject
{
    private static readonly IReadOnlyDictionary<string, decimal> SuggestedPrices =
        new Dictionary<string, decimal>(StringComparer.Ordinal)
        {
            ["Laptop"] = 999.99m,
            ["Phone"] = 699.00m,
            ["Tablet"] = 449.50m,
            ["Monitor"] = 329.99m,
            ["Keyboard"] = 79.99m,
            ["Headphones"] = 129.00m,
            ["Desk Chair"] = 245.00m
        };

    private readonly LiteDbSalesStore _transactions;
    private readonly SalesAggregationCoordinator _aggregation;
    private readonly ILogger<SalesViewModel> _logger;

    [ObservableProperty]
    public partial string? SelectedProduct { get; set; }

    [ObservableProperty]
    public partial string QuantityText { get; set; }

    [ObservableProperty]
    public partial string UnitPriceText { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; }

    public SalesViewModel(
        LiteDbSalesStore transactions,
        SalesAggregationCoordinator aggregation,
        ILogger<SalesViewModel> logger)
    {
        _transactions = transactions;
        _aggregation = aggregation;
        _logger = logger;
        QuantityText = "1";
        UnitPriceText = string.Empty;
        StatusMessage = "Choose a product and enter the sale details.";
    }

    public IReadOnlyList<string> Products { get; } = SuggestedPrices.Keys.ToArray();

    partial void OnSelectedProductChanged(string? value)
    {
        UnitPriceText = value is not null && SuggestedPrices.TryGetValue(value, out var price)
            ? price.ToString("0.00", CultureInfo.CurrentCulture)
            : string.Empty;
    }

    [RelayCommand]
    private async Task AddTransactionAsync()
    {
        if (SelectedProduct is not { } product || !SuggestedPrices.ContainsKey(product))
        {
            StatusMessage = "Select a product before saving.";
            return;
        }

        if (!int.TryParse(QuantityText, NumberStyles.Integer, CultureInfo.CurrentCulture, out var quantity)
            || quantity <= 0)
        {
            StatusMessage = "Quantity must be a positive whole number.";
            return;
        }

        const NumberStyles priceStyles = NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign
            | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite;
        if (!decimal.TryParse(UnitPriceText, priceStyles, CultureInfo.CurrentCulture, out var price)
            || price <= 0 || price > decimal.MaxValue / quantity)
        {
            StatusMessage = "Unit price must be a positive number that can be multiplied by the quantity.";
            return;
        }

        try
        {
            await _transactions.InitializeAndSeedAsync();
        }
        catch (Exception exception) when (SalesDataErrors.IsExpected(exception))
        {
            _logger.LogError(exception, "Could not initialize transaction storage before saving a sale.");
            StatusMessage = "Transaction storage could not be initialized. The sale was not saved; try again.";
            return;
        }

        var sale = new ProductSaleTransaction
        {
            Timestamp = DateTime.Now,
            Product = product,
            Quantity = quantity,
            Price = price
        };

        try
        {
            await _transactions.InsertAsync(sale);
        }
        catch (Exception exception) when (SalesDataErrors.IsExpected(exception))
        {
            _logger.LogError(exception, "Could not save the sale to LiteDB.");
            StatusMessage = "The sale could not be saved. Check storage and try again.";
            return;
        }

        var saved = $"Saved {product}: {quantity} x {price.ToString("C2", CultureInfo.CurrentCulture)}"
            + $" ({sale.Total.ToString("C2", CultureInfo.CurrentCulture)}).";
        try
        {
            await _aggregation.RefreshAsync();
            StatusMessage = $"{saved} Dashboard updated.";
        }
        catch (Exception exception) when (SalesDataErrors.IsExpected(exception))
        {
            _logger.LogError(exception, "The sale was saved, but its dashboard refresh failed.");
            StatusMessage = $"{saved} Dashboard refresh pending; the background service will retry.";
        }
    }
}
