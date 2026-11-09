namespace MauiBackgroundSalesDemo.Models;

public abstract record SalesChartRow;

public sealed record SalesChartHeading(string Title) : SalesChartRow;

public sealed record SalesChartItem(string Name, string Detail, double Progress) : SalesChartRow;
