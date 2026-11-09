using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MauiBackgroundSalesDemo.Services;

public sealed class SalesAggregationBackgroundService : BackgroundService
{
    private readonly SalesAggregationCoordinator _aggregation;
    private readonly ILogger<SalesAggregationBackgroundService> _logger;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private bool _started;

    public SalesAggregationBackgroundService(
        SalesAggregationCoordinator aggregation,
        ILogger<SalesAggregationBackgroundService> logger)
    {
        _aggregation = aggregation;
        _logger = logger;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_started)
            {
                await base.StartAsync(cancellationToken).ConfigureAwait(false);
                _started = true;
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_started)
            {
                await base.StopAsync(cancellationToken).ConfigureAwait(false);
                _started = false;
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            try
            {
                await _aggregation.EnsureInitializedAsync(stoppingToken).ConfigureAwait(false);
                _logger.LogInformation("Initial sales summary completed.");
            }
            catch (Exception exception) when (SalesDataErrors.IsExpected(exception))
            {
                _logger.LogError(exception, "Initial sales summary failed; the timer will retry.");
            }

            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                try
                {
                    var count = await _aggregation.RefreshAsync(stoppingToken).ConfigureAwait(false);
                    _logger.LogInformation(
                        "15-second timer refresh completed at {CompletedAt:O}; {TransactionCount} transactions.",
                        DateTimeOffset.Now, count);
                }
                catch (Exception exception) when (SalesDataErrors.IsExpected(exception))
                {
                    _logger.LogError(exception, "The timer could not refresh sales summaries; it will retry.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Sales aggregation stopped.");
        }
    }
}
