using LiteDB;
using MauiBackgroundSalesDemo.Models;

namespace MauiBackgroundSalesDemo.Services;

public sealed class LiteDbSalesStore
{
    private readonly string _databasePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _initialized;

    public LiteDbSalesStore(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        _databasePath = Path.GetFullPath(databasePath);
    }

    public Task InitializeAndSeedAsync(CancellationToken cancellationToken = default) =>
        WithDatabaseAsync(database =>
        {
            var sales = database.GetCollection<ProductSaleTransaction>("sales");
            sales.EnsureIndex(sale => sale.Timestamp);
            sales.EnsureIndex(sale => sale.Product);

            if (sales.Count() == 0)
            {
                if (!database.BeginTrans())
                {
                    throw new InvalidOperationException("Could not begin the seed transaction.");
                }

                var committed = false;
                try
                {
                    foreach (var sale in SalesSeedData.Create(DateTime.Now))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        sales.Insert(sale);
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    committed = database.Commit();
                    if (!committed)
                    {
                        throw new InvalidOperationException("Could not commit the seed transaction.");
                    }
                }
                finally
                {
                    if (!committed)
                    {
                        database.Rollback();
                    }
                }
            }

            _initialized = true;
            return true;
        }, cancellationToken, allowCreate: true);

    public Task InsertAsync(ProductSaleTransaction sale, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sale);
        if (string.IsNullOrWhiteSpace(sale.Product) || sale.Quantity <= 0 || sale.Price <= 0)
        {
            throw new ArgumentException("A sale requires a product, positive quantity, and positive price.", nameof(sale));
        }

        return WithDatabaseAsync(database =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            database.GetCollection<ProductSaleTransaction>("sales").Insert(sale);
            return true;
        }, cancellationToken);
    }

    public Task<IReadOnlyList<ProductSaleTransaction>> GetAllAsync(CancellationToken cancellationToken = default) =>
        WithDatabaseAsync<IReadOnlyList<ProductSaleTransaction>>(database =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return database.GetCollection<ProductSaleTransaction>("sales").FindAll().ToList();
        }, cancellationToken);

    private async Task<T> WithDatabaseAsync<T>(
        Func<LiteDatabase, T> operation,
        CancellationToken cancellationToken,
        bool allowCreate = false)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var directory = Path.GetDirectoryName(_databasePath)
                    ?? throw new InvalidOperationException("The LiteDB path has no parent directory.");
                Directory.CreateDirectory(directory);
                if (!File.Exists(_databasePath) && (!allowCreate || _initialized))
                {
                    throw new FileNotFoundException("The sales transactions database is missing.", _databasePath);
                }

                using var database = new LiteDatabase(_databasePath);
                return operation(database);
            }, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }
}
