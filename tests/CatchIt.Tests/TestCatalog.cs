using CatchIt.Core.Data;
using Microsoft.Data.Sqlite;

namespace CatchIt.Tests;

/// <summary>
/// Small helpers for tests that need hand-made products and listings instead of the full demo seed.
/// </summary>
internal static class TestCatalog
{
    /// <summary>Adds a product (creating its brand if needed) and returns the product's id.</summary>
    public static long AddProduct(Database db, string name, string brand = "TestBrand", int tier = 2, string keywords = "")
    {
        using var conn = db.Open();
        Exec(conn, "INSERT OR IGNORE INTO Brands (Name, QualityTier) VALUES ($n, $t)", ("$n", brand), ("$t", tier));
        Exec(conn, "INSERT INTO Products (Name, Keywords, BrandId) VALUES ($n, $k, (SELECT Id FROM Brands WHERE Name = $b))",
            ("$n", name), ("$k", keywords), ("$b", brand));
        return Scalar(conn, "SELECT last_insert_rowid()");
    }

    /// <summary>Adds a listing for a product on a marketplace (created if needed) and returns the listing's id.</summary>
    public static long AddListing(Database db, long productId, string marketplace = "TestMarket", double? currentPrice = 50.0,
        double sellerRating = 4.0)
    {
        using var conn = db.Open();
        Exec(conn, "INSERT OR IGNORE INTO Marketplaces (Name) VALUES ($n)", ("$n", marketplace));
        Exec(conn, "INSERT INTO Listings (ProductId, MarketplaceId, SellerRating, CurrentPrice) " +
                   "VALUES ($p, (SELECT Id FROM Marketplaces WHERE Name = $m), $r, $c)",
            ("$p", productId), ("$m", marketplace), ("$r", sellerRating), ("$c", (object?)currentPrice ?? DBNull.Value));
        return Scalar(conn, "SELECT last_insert_rowid()");
    }

    /// <summary>Adds one stored history point directly, bypassing the service.</summary>
    public static void AddPoint(Database db, long listingId, DateOnly date, double price)
    {
        using var conn = db.Open();
        Exec(conn, "INSERT INTO PriceHistory (ListingId, Date, Price) VALUES ($l, $d, $p)",
            ("$l", listingId), ("$d", date.ToString("yyyy-MM-dd")), ("$p", price));
    }

    /// <summary>Counts the rows a query returns, for quick assertions on table sizes.</summary>
    public static long Count(Database db, string sql)
    {
        using var conn = db.Open();
        return Scalar(conn, sql);
    }

    private static void Exec(SqliteConnection conn, string sql, params (string, object)[] args)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (k, v) in args) cmd.Parameters.AddWithValue(k, v);
        cmd.ExecuteNonQuery();
    }

    private static long Scalar(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return Convert.ToInt64(cmd.ExecuteScalar());
    }
}
