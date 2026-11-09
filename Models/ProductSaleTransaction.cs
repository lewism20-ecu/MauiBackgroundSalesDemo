using LiteDB;

namespace MauiBackgroundSalesDemo.Models;

public sealed class ProductSaleTransaction
{
    [BsonId]
    public int Id { get; set; }

    public DateTime Timestamp { get; set; }

    public string Product { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public decimal Price { get; set; }

    [BsonIgnore]
    public decimal Total => Quantity * Price;
}
