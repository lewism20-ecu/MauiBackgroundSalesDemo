using MauiBackgroundSalesDemo.Models;
using Microsoft.Data.Sqlite;

namespace MauiBackgroundSalesDemo.Services;

public sealed class SqliteSalesAggregateStore
{
    private readonly string _databasePath;
    private readonly string _connectionString;

    public SqliteSalesAggregateStore(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        _databasePath = Path.GetFullPath(databasePath);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
            DefaultTimeout = 10
        }.ToString();
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        var directory = Path.GetDirectoryName(_databasePath)
            ?? throw new InvalidOperationException("The SQLite path has no parent directory.");
        Directory.CreateDirectory(directory);
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS DailySalesSummary (
                DateKey TEXT PRIMARY KEY,
                TotalRevenue REAL NOT NULL,
                TransactionCount INTEGER NOT NULL
            );
            CREATE TABLE IF NOT EXISTS ProductSalesSummary (
                Product TEXT PRIMARY KEY,
                TotalRevenue REAL NOT NULL,
                UnitsSold INTEGER NOT NULL
            );
            CREATE TABLE IF NOT EXISTS CategorySalesSummary (
                Category TEXT PRIMARY KEY,
                TotalRevenue REAL NOT NULL,
                UnitsSold INTEGER NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }, cancellationToken);

    public Task ReplaceAsync(SalesSummarySnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var connection = OpenConnection();
            using var transaction = connection.BeginTransaction();
            using (var clear = connection.CreateCommand())
            {
                clear.Transaction = transaction;
                clear.CommandText = """
                    DELETE FROM DailySalesSummary;
                    DELETE FROM ProductSalesSummary;
                    DELETE FROM CategorySalesSummary;
                    """;
                clear.ExecuteNonQuery();
            }

            InsertRows(connection, transaction,
                "INSERT INTO DailySalesSummary (DateKey, TotalRevenue, TransactionCount) VALUES ($key, $revenue, $count);",
                snapshot.Daily.Select(row => (row.DateKey, row.TotalRevenue, row.TransactionCount)), cancellationToken);
            InsertRows(connection, transaction,
                "INSERT INTO ProductSalesSummary (Product, TotalRevenue, UnitsSold) VALUES ($key, $revenue, $count);",
                snapshot.Products.Select(row => (row.Product, row.TotalRevenue, row.UnitsSold)), cancellationToken);
            InsertRows(connection, transaction,
                "INSERT INTO CategorySalesSummary (Category, TotalRevenue, UnitsSold) VALUES ($key, $revenue, $count);",
                snapshot.Categories.Select(row => (row.Category, row.TotalRevenue, row.UnitsSold)), cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            transaction.Commit();
        }, cancellationToken);
    }

    public Task<SalesSummarySnapshot> ReadAsync(CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction(deferred: true);
        var daily = ReadRows(connection, transaction,
            "SELECT DateKey, TotalRevenue, TransactionCount FROM DailySalesSummary ORDER BY DateKey;",
            (key, revenue, count) => new DailySalesSummary(key, revenue, count), cancellationToken);
        var products = ReadRows(connection, transaction,
            "SELECT Product, TotalRevenue, UnitsSold FROM ProductSalesSummary ORDER BY TotalRevenue DESC, Product;",
            (key, revenue, count) => new ProductSalesSummary(key, revenue, count), cancellationToken);
        var categories = ReadRows(connection, transaction,
            "SELECT Category, TotalRevenue, UnitsSold FROM CategorySalesSummary ORDER BY TotalRevenue DESC, Category;",
            (key, revenue, count) => new CategorySalesSummary(key, revenue, count), cancellationToken);
        transaction.Commit();
        return new SalesSummarySnapshot(daily, products, categories);
    }, cancellationToken);

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        var opened = false;
        try
        {
            connection.Open();
            opened = true;
            return connection;
        }
        finally
        {
            if (!opened)
                connection.Dispose();
        }
    }

    private static void InsertRows(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string sql,
        IEnumerable<(string Key, decimal Revenue, long Count)> rows,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        var key = command.Parameters.Add("$key", SqliteType.Text);
        var revenue = command.Parameters.Add("$revenue", SqliteType.Real);
        var count = command.Parameters.Add("$count", SqliteType.Integer);
        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            key.Value = row.Key;
            revenue.Value = (double)row.Revenue;
            count.Value = row.Count;
            command.ExecuteNonQuery();
        }
    }

    private static List<T> ReadRows<T>(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string sql,
        Func<string, decimal, long, T> makeRow,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        var rows = new List<T>();
        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            rows.Add(makeRow(reader.GetString(0), Convert.ToDecimal(reader.GetDouble(1)), reader.GetInt64(2)));
        }

        return rows;
    }
}
