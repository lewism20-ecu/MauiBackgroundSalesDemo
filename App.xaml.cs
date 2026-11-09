using MauiBackgroundSalesDemo.Services;

namespace MauiBackgroundSalesDemo;

public partial class App : Application
{
	private readonly IServiceProvider _services;
	private readonly SalesAggregationBackgroundService _backgroundService;

	public App(IServiceProvider services, SalesAggregationBackgroundService backgroundService)
	{
		InitializeComponent();
		_services = services;
		_backgroundService = backgroundService;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var window = new Window(_services.GetRequiredService<AppShell>());
		window.Created += OnWindowCreated;
		window.Destroying += OnWindowDestroying;
		return window;
	}

	private async void OnWindowCreated(object? sender, EventArgs e) =>
		await _backgroundService.StartAsync(CancellationToken.None);

	private async void OnWindowDestroying(object? sender, EventArgs e) =>
		await _backgroundService.StopAsync(CancellationToken.None);
}