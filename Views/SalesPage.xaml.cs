using MauiBackgroundSalesDemo.ViewModels;

namespace MauiBackgroundSalesDemo.Views;

public partial class SalesPage : ContentPage
{
    public SalesPage(SalesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
