using MauiBackgroundSalesDemo.Services;
using MauiBackgroundSalesDemo.ViewModels;
using MauiBackgroundSalesDemo.Views;
using Microsoft.Extensions.Logging;
#if DEBUG
using Microsoft.Maui.DevFlow.Agent;
#endif

namespace MauiBackgroundSalesDemo;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if DEBUG
		builder.AddMauiDevFlowAgent();
#endif

		builder.Services.AddSingleton(_ => new LiteDbSalesStore(
			Path.Combine(FileSystem.AppDataDirectory, "sales-transactions.db")));
		builder.Services.AddSingleton(_ => new SqliteSalesAggregateStore(
			Path.Combine(FileSystem.AppDataDirectory, "sales-aggregates.db")));
		builder.Services.AddSingleton<SalesAggregationCoordinator>();
		builder.Services.AddSingleton<SalesAggregationBackgroundService>();
		builder.Services.AddSingleton<DashboardViewModel>();
		builder.Services.AddSingleton<SalesViewModel>();
		builder.Services.AddSingleton<DashboardPage>();
		builder.Services.AddSingleton<SalesPage>();
		builder.Services.AddSingleton<AppShell>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		var app = builder.Build();
		app.Services.GetRequiredService<SalesAggregationBackgroundService>()
			.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
		return app;
	}
}
