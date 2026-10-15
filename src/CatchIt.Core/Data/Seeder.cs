using Microsoft.Data.Sqlite;

namespace CatchIt.Core.Data;

/// <summary>
/// Populates the database with simulated demo data. Safe to run repeatedly:
/// existing records are skipped, so no duplicates are created.
/// All prices and seller ratings are SIMULATED.
/// </summary>
public class Seeder
{
    public const int BcryptCost = 10;
    private const int HistoryDays = 400;

    private readonly Database _db;
    private readonly Random _rng;

    public Seeder(Database db, int randomSeed = 322)
    {
        _db = db;
        _rng = new Random(randomSeed);
    }

    private static readonly string[] Marketplaces = { "ShopMart", "BargainBay", "MegaStore", "QuickBuy" };

    /// <summary>The demo marketplace names, shared with <see cref="BulkImporter"/>.</summary>
    internal static IReadOnlyList<string> MarketplaceNames => Marketplaces;

    private static readonly (string Name, int Tier)[] Brands =
    {
        ("Apex", 3), ("Zenith", 3), ("Orbit", 2), ("Pioneer", 2), ("ValueTech", 1), ("Generic", 1),
    };

    // (Product, Keywords, Brand, base price)
    private static readonly (string Name, string Keywords, string Brand, double Price)[] Products =
    {
        ("Apex Wireless Headphones", "headphones audio wireless", "Apex", 180),
        ("Orbit Wireless Headphones", "headphones audio wireless", "Orbit", 95),
        ("ValueTech Wireless Headphones", "headphones audio wireless", "ValueTech", 35),
        ("Zenith 4K Monitor 27in", "monitor display screen 4k", "Zenith", 420),
        ("Pioneer 4K Monitor 27in", "monitor display screen 4k", "Pioneer", 290),
        ("Generic 1080p Monitor 24in", "monitor display screen 1080p", "Generic", 120),
        ("Apex Mechanical Keyboard", "keyboard gaming mechanical", "Apex", 140),
        ("Pioneer Mechanical Keyboard", "keyboard gaming mechanical", "Pioneer", 80),
        ("Generic Membrane Keyboard", "keyboard office membrane", "Generic", 18),
        ("Orbit Bluetooth Speaker", "speaker audio bluetooth", "Orbit", 60),
        ("ValueTech Bluetooth Speaker", "speaker audio bluetooth", "ValueTech", 22),
    };

    // Curated: product -> cheaper, lower-tier alternative
    private static readonly (string Product, string Alternative)[] AlternativeMap =
    {
        ("Apex Wireless Headphones", "Orbit Wireless Headphones"),
        ("Orbit Wireless Headphones", "ValueTech Wireless Headphones"),
        ("Zenith 4K Monitor 27in", "Pioneer 4K Monitor 27in"),
        ("Pioneer 4K Monitor 27in", "Generic 1080p Monitor 24in"),
        ("Apex Mechanical Keyboard", "Pioneer Mechanical Keyboard"),
        ("Pioneer Mechanical Keyboard", "Generic Membrane Keyboard"),
        ("Orbit Bluetooth Speaker", "ValueTech Bluetooth Speaker"),
    };

    public void Run()
    {
        _db.Initialize();
        using var conn = _db.Open();
        using var tx = conn.BeginTransaction();

        SeedDemoUser(conn, tx);
        foreach (var m in Marketplaces)
            Exec(conn, tx, "INSERT OR IGNORE INTO Marketplaces (Name) VALUES ($n)", ("$n", m));
        foreach (var (n, t) in Brands)
            Exec(conn, tx, "INSERT OR IGNORE INTO Brands (Name, QualityTier) VALUES ($n, $t)", ("$n", n), ("$t", t));

        foreach (var p in Products)
        {
            Exec(conn, tx,
                "INSERT OR IGNORE INTO Products (Name, Keywords, BrandId) " +
                "VALUES ($n, $k, (SELECT Id FROM Brands WHERE Name = $b))",
                ("$n", p.Name), ("$k", p.Keywords), ("$b", p.Brand));
        }

        foreach (var (a, b) in AlternativeMap)
        {
            Exec(conn, tx,
                "INSERT OR IGNORE INTO Alternatives (ProductId, AlternativeProductId) " +
                "VALUES ((SELECT Id FROM Products WHERE Name = $a), (SELECT Id FROM Products WHERE Name = $b))",
                ("$a", a), ("$b", b));
        }

        SeedListingsAndHistory(conn, tx);
        tx.Commit();
    }

    private static void SeedDemoUser(SqliteConnection conn, SqliteTransaction tx)
    {
        // Demo account. Credentials are documented in the README (simulated data only).
        var hash = BCrypt.Net.BCrypt.HashPassword("Demo1234!", BcryptCost);
        Exec(conn, tx,
            "INSERT OR IGNORE INTO Users (Name, Email, PasswordHash, IsConfirmed) " +
            "VALUES ('Demo User', 'demo@catchit.test', $h, 1)",
            ("$h", hash));
    }

    private void SeedListingsAndHistory(SqliteConnection conn, SqliteTransaction tx)
    {
        var products = Query(conn, tx, "SELECT Id, Name FROM Products");
        var marketplaceIds = Query(conn, tx, "SELECT Id, Name FROM Marketplaces").Select(m => m.Id).ToList();
        var basePrices = Products.ToDictionary(p => p.Name, p => p.Price);

        // Listings, simulated seasonal history (weekly points plus the absolute low and high)
        // and the price extremes are all created by the shared writer, as in a bulk import.
        using var writer = new ListingWriter(conn, tx, _rng, DateOnly.FromDateTime(DateTime.Today), HistoryDays, marketplaceIds);

        foreach (var (productId, productName) in products)
        {
            // Already seeded: leave existing listings and history untouched.
            if (Scalar(conn, tx, "SELECT COUNT(*) FROM Listings WHERE ProductId = $p", ("$p", productId)) > 0)
                continue;

            writer.AddListingsForProduct(productId, basePrices[productName]);
        }
    }

    private static void Exec(SqliteConnection conn, SqliteTransaction tx, string sql, params (string, object)[] args)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        foreach (var (k, v) in args) cmd.Parameters.AddWithValue(k, v);
        cmd.ExecuteNonQuery();
    }

    private static long Scalar(SqliteConnection conn, SqliteTransaction tx, string sql, params (string, object)[] args)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        foreach (var (k, v) in args) cmd.Parameters.AddWithValue(k, v);
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    private static List<(long Id, string Name)> Query(SqliteConnection conn, SqliteTransaction tx, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        using var r = cmd.ExecuteReader();
        var list = new List<(long, string)>();
        while (r.Read()) list.Add((r.GetInt64(0), r.GetString(1)));
        return list;
    }
}
