using LiteDB;
using Microsoft.Data.Sqlite;

namespace MauiBackgroundSalesDemo.Services;

internal static class SalesDataErrors
{
    public static bool IsExpected(Exception exception) =>
        exception is LiteException or SqliteException or IOException or UnauthorizedAccessException or OverflowException;
}
