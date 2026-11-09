using System.Diagnostics;
using MauiBackgroundSalesDemo.Models;
using MauiBackgroundSalesDemo.Services;
using Microsoft.Extensions.Logging;

namespace SalesDemo.Tests;

public class SalesBackgroundServiceTests
{
    [Fact]
    public async Task RepeatedLifecycleEventsDoNotDuplicateTheServiceAndAllowRestart()
    {
        using var database = new TestDatabase();
        var logger = new TimerLogger();
        using var service = new SalesAggregationBackgroundService(database.Coordinator, logger);

        await service.StopAsync(CancellationToken.None);
        await Task.WhenAll(Enumerable.Range(0, 4)
            .Select(_ => service.StartAsync(CancellationToken.None)));
        await logger.InitialCompleted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var firstRun = service.ExecuteTask;
        Assert.NotNull(firstRun);

        await service.StartAsync(CancellationToken.None);
        Assert.Same(firstRun, service.ExecuteTask);

        await Task.WhenAll(Enumerable.Range(0, 4)
            .Select(_ => service.StopAsync(CancellationToken.None)));
        Assert.True(firstRun.IsCompleted);
        Assert.True(logger.Stopped);

        await service.StartAsync(CancellationToken.None);
        var secondRun = service.ExecuteTask;
        Assert.NotNull(secondRun);
        Assert.NotSame(firstRun, secondRun);
        await service.StopAsync(CancellationToken.None);
        Assert.True(secondRun.IsCompleted);
    }

    [Fact]
    public async Task TimerRebuildsTheSnapshotAfterFifteenSecondsAndStopsOnCancellation()
    {
        using var database = new TestDatabase();
        var logger = new TimerLogger();
        using var service = new SalesAggregationBackgroundService(database.Coordinator, logger);
        var elapsed = Stopwatch.StartNew();
        await service.StartAsync(CancellationToken.None);

        try
        {
            await database.Coordinator.EnsureInitializedAsync();
            var before = await database.Aggregates.ReadAsync();
            await database.Transactions.InsertAsync(new ProductSaleTransaction
            {
                Timestamp = DateTime.Now,
                Product = "Laptop",
                Quantity = 2,
                Price = 25m
            });

            var log = await logger.TimerCompleted.Task.WaitAsync(TimeSpan.FromSeconds(30));
            elapsed.Stop();
            var after = await database.Aggregates.ReadAsync();

            Assert.True(elapsed.Elapsed >= TimeSpan.FromSeconds(15), $"Timer fired after {elapsed.Elapsed}.");
            Assert.Contains("15-second timer refresh completed", log);
            Assert.Equal(
                decimal.Round(before.Products.Single(row => row.Product == "Laptop").TotalRevenue + 50m, 2),
                decimal.Round(after.Products.Single(row => row.Product == "Laptop").TotalRevenue, 2));
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }

        Assert.True(logger.Stopped);
    }

    private sealed class TimerLogger : ILogger<SalesAggregationBackgroundService>
    {
        public TaskCompletionSource<string> TimerCompleted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource InitialCompleted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool Stopped { get; private set; }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            if (message == "Initial sales summary completed.")
                InitialCompleted.TrySetResult();
            if (message.StartsWith("15-second timer refresh completed", StringComparison.Ordinal))
                TimerCompleted.TrySetResult(message);
            if (message == "Sales aggregation stopped.")
                Stopped = true;
        }
    }
}
