using CatchIt.Core.Data;
using CatchIt.Core.Pricing;
using Microsoft.Data.Sqlite;

namespace CatchIt.Tests;

/// <summary>
/// Tests for the compact <c>PriceHistory</c> table layout (primary key on listing and date, stored
/// without a separate row id), and for upgrading a database that still has the original, larger layout.
/// Each test gets its own throwaway database file.
/// </summary>
public class PriceHistoryLayoutTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 10, 12);

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"catchit-test-{Guid.NewGuid():N}.db");
    private readonly Database _db;

    public PriceHistoryLayoutTests()
    {
        _db = new Database(_path);
        _db.Initialize();
    }

    public void Dispose()
    {
        // SQLite keeps pooled connections open, which would stop the file being deleted.
        SqliteConnection.ClearAllPools();
        if (File.Exists(_path)) File.Delete(_path);
    }

    private long Count(string sql) => TestCatalog.Count(_db, sql);

    private void Run(string sql)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private List<string> PriceHistoryColumns()
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM pragma_table_info('PriceHistory') ORDER BY cid";
        using var reader = cmd.ExecuteReader();
        var columns = new List<string>();
        while (reader.Read()) columns.Add(reader.GetString(0));
        return columns;
    }

    private string PriceHistorySql()
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT sql FROM sqlite_master WHERE name = 'PriceHistory'";
        return (string)cmd.ExecuteScalar()!;
    }

    /// <summary>Every stored point, in a fixed order, for comparing before and after an upgrade.</summary>
    private List<(long Listing, string Date, double Price)> AllPoints()
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT ListingId, Date, Price FROM PriceHistory ORDER BY ListingId, Date";
        using var reader = cmd.ExecuteReader();
        var rows = new List<(long, string, double)>();
        while (reader.Read()) rows.Add((reader.GetInt64(0), reader.GetString(1), reader.GetDouble(2)));
        return rows;
    }

    /// <summary>
    /// Turns the database back into one with the ORIGINAL price history table (an <c>Id</c> column plus a
    /// separate unique index) and fills it with daily points, as a database made before the upgrade would have.
    /// </summary>
    private long[] MakeOldLayoutDatabase(int listings, int daysEach)
    {
        var ids = Enumerable.Range(0, listings)
            .Select(i => TestCatalog.AddListing(_db, TestCatalog.AddProduct(_db, $"Old Widget {i}"), $"Market{i % 3}"))
            .ToArray();

        Run("DROP TABLE PriceHistory");
        Run("""
            CREATE TABLE PriceHistory (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ListingId INTEGER NOT NULL REFERENCES Listings(Id),
                Date TEXT NOT NULL,
                Price REAL NOT NULL CHECK (Price > 0),
                UNIQUE (ListingId, Date)
            );
            """);

        using var conn = _db.Open();
        using var tx = conn.BeginTransaction();
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "INSERT INTO PriceHistory (ListingId, Date, Price) VALUES ($l, $d, $p)";
        var pl = cmd.CreateParameter(); pl.ParameterName = "$l"; cmd.Parameters.Add(pl);
        var pd = cmd.CreateParameter(); pd.ParameterName = "$d"; cmd.Parameters.Add(pd);
        var pp = cmd.CreateParameter(); pp.ParameterName = "$p"; cmd.Parameters.Add(pp);
        foreach (var id in ids)
            for (var d = 0; d < daysEach; d++)
            {
                pl.Value = id;
                pd.Value = Today.AddDays(-d).ToString("yyyy-MM-dd");
                pp.Value = 20 + (id * 7 + d * 13) % 90 + 0.25;
                cmd.ExecuteNonQuery();
            }
        tx.Commit();
        return ids;
    }

    // ---- The layout of a new database ----

    [Fact]
    public void NewDatabase_PriceHistoryHasOnlyListingDateAndPrice()
    {
        Assert.Equal(new[] { "ListingId", "Date", "Price" }, PriceHistoryColumns());
    }

    [Fact]
    public void NewDatabase_PriceHistoryIsStoredWithoutARowId()
    {
        Assert.Contains("WITHOUT ROWID", PriceHistorySql());
    }

    [Fact]
    public void ThePrimaryKeyRejectsASecondPointForTheSameListingAndDay()
    {
        var listing = TestCatalog.AddListing(_db, TestCatalog.AddProduct(_db, "Widget"));
        TestCatalog.AddPoint(_db, listing, Today, 40);

        Assert.Throws<SqliteException>(() => TestCatalog.AddPoint(_db, listing, Today, 41));
        Assert.Equal(1, Count("SELECT COUNT(*) FROM PriceHistory"));
    }

    [Fact]
    public void InsertOrIgnore_SkipsADuplicateWithoutAnError_AndKeepsTheFirstPrice()
    {
        var listing = TestCatalog.AddListing(_db, TestCatalog.AddProduct(_db, "Widget"));
        TestCatalog.AddPoint(_db, listing, Today, 40);

        Run($"INSERT OR IGNORE INTO PriceHistory (ListingId, Date, Price) VALUES ({listing}, '2026-10-12', 99)");

        Assert.Equal(40, AllPoints().Single().Price);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ThePriceMustBePositive(double price)
    {
        var listing = TestCatalog.AddListing(_db, TestCatalog.AddProduct(_db, "Widget"));

        Assert.Throws<SqliteException>(() => TestCatalog.AddPoint(_db, listing, Today, price));
    }

    [Fact]
    public void AHistoryPointNeedsAnExistingListing()
    {
        Assert.Throws<SqliteException>(() => TestCatalog.AddPoint(_db, 99999, Today, 40));
    }

    [Fact]
    public void ReadingOneListingsHistory_UsesThePrimaryKeyNotAScan()
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "EXPLAIN QUERY PLAN SELECT Date, Price FROM PriceHistory WHERE ListingId = 5 AND Date >= '2026-01-01'";
        using var reader = cmd.ExecuteReader();
        var plan = new List<string>();
        while (reader.Read()) plan.Add(reader.GetString(3));

        Assert.Contains(plan, line => line.Contains("SEARCH") && line.Contains("PRIMARY KEY"));
        Assert.DoesNotContain(plan, line => line.Contains("SCAN"));
    }

    // ---- Upgrading a database that has the original layout ----

    [Fact]
    public void Upgrade_ConvertsTheTableToTheCompactLayout()
    {
        MakeOldLayoutDatabase(listings: 3, daysEach: 120);
        Assert.Contains("Id", PriceHistoryColumns());

        _db.Initialize();

        Assert.Equal(new[] { "ListingId", "Date", "Price" }, PriceHistoryColumns());
        Assert.Contains("WITHOUT ROWID", PriceHistorySql());
    }

    [Fact]
    public void Upgrade_KeepsEveryPointExactly()
    {
        MakeOldLayoutDatabase(listings: 4, daysEach: 200);
        var before = AllPoints();
        Assert.Equal(800, before.Count);

        _db.Initialize();

        Assert.Equal(before, AllPoints());
    }

    [Fact]
    public void Upgrade_AnEmptyOldTableIsConvertedToo()
    {
        MakeOldLayoutDatabase(listings: 2, daysEach: 0);

        _db.Initialize();

        Assert.Equal(new[] { "ListingId", "Date", "Price" }, PriceHistoryColumns());
        Assert.Equal(0, Count("SELECT COUNT(*) FROM PriceHistory"));
    }

    [Fact]
    public void Upgrade_HappensOnlyOnce()
    {
        MakeOldLayoutDatabase(listings: 2, daysEach: 50);
        _db.Initialize();
        var afterFirst = AllPoints();

        _db.Initialize();
        _db.Initialize();

        Assert.Equal(afterFirst, AllPoints());
        Assert.Equal(new[] { "ListingId", "Date", "Price" }, PriceHistoryColumns());
    }

    [Fact]
    public void Upgrade_TheConvertedTableStillEnforcesItsRules()
    {
        var ids = MakeOldLayoutDatabase(listings: 2, daysEach: 10);
        _db.Initialize();

        Assert.Throws<SqliteException>(() => TestCatalog.AddPoint(_db, ids[0], Today, 55));           // duplicate day
        Assert.Throws<SqliteException>(() => TestCatalog.AddPoint(_db, ids[0], Today.AddDays(1), 0)); // bad price
        Assert.Throws<SqliteException>(() => TestCatalog.AddPoint(_db, 99999, Today, 40));            // no such listing
    }

    [Fact]
    public void Upgrade_ThePriceServicesKeepWorkingOnTheConvertedTable()
    {
        var ids = MakeOldLayoutDatabase(listings: 1, daysEach: 100);
        _db.Initialize();
        var history = new PriceHistoryService(_db);

        Assert.Equal(11, history.GetPoints(ids[0], Today.AddDays(-10), Today).Count);
        Assert.NotNull(history.GetAverage(ids[0], 90, Today));

        history.RecordPrice(ids[0], Today.AddDays(1), 5); // a new low, which is stored as a point
        Assert.Contains(history.GetPoints(ids[0], Today.AddDays(1), Today.AddDays(1)), p => p.Price == 5);
        Assert.Equal(5, history.GetExtremes(ids[0])!.LowPrice);
    }

    [Fact]
    public void Upgrade_MakesTheDatabaseFileSmaller()
    {
        MakeOldLayoutDatabase(listings: 100, daysEach: 400);   // 40,000 daily rows in the old layout
        SqliteConnection.ClearAllPools();
        var before = new FileInfo(_path).Length;

        _db.Initialize();
        SqliteConnection.ClearAllPools();
        var after = new FileInfo(_path).Length;

        Assert.True(after < before * 0.8, $"expected the file to shrink by at least 20%: {before:N0} -> {after:N0} bytes");
    }

    [Fact]
    public void Upgrade_IfItFailsPartway_TheOldTableAndItsDataAreLeftUntouched()
    {
        MakeOldLayoutDatabase(listings: 2, daysEach: 30);
        var before = AllPoints();

        // A leftover table with the name the upgrade uses for its copy, but the wrong columns, makes the copy fail.
        Run("CREATE TABLE PriceHistory_compact (unrelated TEXT)");

        Assert.Throws<SqliteException>(() => _db.Initialize());

        Assert.Contains("Id", PriceHistoryColumns());      // still the old layout
        Assert.Equal(before, AllPoints());                  // and nothing was lost
    }

    [Fact]
    public void Upgrade_DemoDataSeedsAndSearchesNormallyAfterwards()
    {
        MakeOldLayoutDatabase(listings: 1, daysEach: 20);
        _db.Initialize();

        new Seeder(_db).Run();

        Assert.True(Count("SELECT COUNT(*) FROM PriceExtremes") > 0);
        Assert.Equal(3, new CatchIt.Core.Search.ProductSearchService(_db).Search("headphones").TotalMatches);
    }
}
