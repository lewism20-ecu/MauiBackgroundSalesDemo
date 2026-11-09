using MauiBackgroundSalesDemo.Services;

namespace SalesDemo.Tests;

internal sealed class TestDatabase : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"SalesDemoTests-{Guid.NewGuid():N}");

    public TestDatabase()
    {
        Directory.CreateDirectory(_directory);
        TransactionsPath = Path.Combine(_directory, "sales-transactions.db");
        AggregatesPath = Path.Combine(_directory, "sales-aggregates.db");
        Transactions = new LiteDbSalesStore(TransactionsPath);
        Aggregates = new SqliteSalesAggregateStore(AggregatesPath);
        Coordinator = new SalesAggregationCoordinator(Transactions, Aggregates);
    }

    public string TransactionsPath { get; }
    public string AggregatesPath { get; }
    public LiteDbSalesStore Transactions { get; }
    public SqliteSalesAggregateStore Aggregates { get; }
    public SalesAggregationCoordinator Coordinator { get; }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
