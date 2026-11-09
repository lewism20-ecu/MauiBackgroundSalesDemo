namespace MauiBackgroundSalesDemo.Services;

public sealed class SalesAggregationCoordinator
{
    private readonly LiteDbSalesStore _transactions;
    private readonly SqliteSalesAggregateStore _aggregates;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _initialized;
    private int _lastTransactionCount;

    public SalesAggregationCoordinator(LiteDbSalesStore transactions, SqliteSalesAggregateStore aggregates)
    {
        _transactions = transactions;
        _aggregates = aggregates;
    }

    public async Task EnsureInitializedAsync(CancellationToken cancellationToken = default) =>
        _ = await RebuildAsync(forceRefresh: false, cancellationToken).ConfigureAwait(false);

    public Task<int> RefreshAsync(CancellationToken cancellationToken = default) =>
        RebuildAsync(forceRefresh: true, cancellationToken);

    private async Task<int> RebuildAsync(bool forceRefresh, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_initialized)
            {
                await _transactions.InitializeAndSeedAsync(cancellationToken).ConfigureAwait(false);
            }

            if (!_initialized || forceRefresh)
            {
                await _aggregates.InitializeAsync(cancellationToken).ConfigureAwait(false);
                var sales = await _transactions.GetAllAsync(cancellationToken).ConfigureAwait(false);
                var snapshot = SalesSummaryBuilder.Build(sales);
                await _aggregates.ReplaceAsync(snapshot, cancellationToken).ConfigureAwait(false);
                _lastTransactionCount = sales.Count;
                _initialized = true;
            }

            return _lastTransactionCount;
        }
        finally
        {
            _gate.Release();
        }
    }
}
