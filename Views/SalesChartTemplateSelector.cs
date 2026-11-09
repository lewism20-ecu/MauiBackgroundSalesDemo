using MauiBackgroundSalesDemo.Models;

namespace MauiBackgroundSalesDemo.Views;

public sealed class SalesChartTemplateSelector : DataTemplateSelector
{
    public DataTemplate? HeadingTemplate { get; set; }

    public DataTemplate? ItemTemplate { get; set; }

    protected override DataTemplate OnSelectTemplate(object item, BindableObject container) =>
        item switch
        {
            SalesChartHeading => HeadingTemplate
                ?? throw new InvalidOperationException("The chart heading template is not configured."),
            SalesChartItem => ItemTemplate
                ?? throw new InvalidOperationException("The chart item template is not configured."),
            _ => throw new InvalidOperationException($"Unexpected chart row type: {item.GetType().FullName}.")
        };
}
