using MauiBackgroundSalesDemo.Views;

namespace MauiBackgroundSalesDemo;

public partial class AppShell : Shell
{
	public AppShell(DashboardPage dashboardPage, SalesPage salesPage)
	{
		InitializeComponent();
		DashboardTab.Content = dashboardPage;
		SalesTab.Content = salesPage;
	}
}
