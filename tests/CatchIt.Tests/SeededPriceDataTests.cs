using CatchIt.Core.Data;
using CatchIt.Core.Pricing;
using CatchIt.Core.Search;
using Microsoft.Data.Sqlite;

namespace CatchIt.Tests;

/// <summary>
/// Checks the demo data the <see cref="Seeder"/> now creates with the compact price history:
/// weekly points, extremes for every listing, and a searchable catalog.
/// </summary>
public class SeededPriceDataTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"catchit-test-{Guid.NewGuid():N}.db");
    private readonly Database _db;

    public SeededPriceDataTests()
    {
        _db = new Database(_path);
        new Seeder(_db).Run();
    }

    public void Dispose()
    {
        // SQLite keeps pooled connections open, which would stop the file being deleted.
        SqliteConnection.ClearAllPools();
        if (File.Exists(_path)) File.Delete(_path);
    }

    private long Count(string sql) => TestCatalog.Count(_db, sql);

    [Fact]
    public void EveryListingHasPriceExtremes()
    {
        Assert.True(Count("SELECT COUNT(*) FROM Listings") > 0);
        Assert.Equal(0, Count("SELECT COUNT(*) FROM Listings l WHERE NOT EXISTS (SELECT 1 FROM PriceExtremes e WHERE e.ListingId = l.Id)"));
    }

    [Fact]
    public void ExtremesAreStoredAsRealHistoryPoints()
    {
        // The lowest and highest days must appear on the chart, so each is also a stored point.
        Assert.Equal(0, Count("""
            SELECT COUNT(*) FROM PriceExtremes e WHERE
                NOT EXISTS (SELECT 1 FROM PriceHistory h WHERE h.ListingId = e.ListingId AND h.Date = e.LowDate AND h.Price = e.LowPrice)
             OR NOT EXISTS (SELECT 1 FROM PriceHistory h WHERE h.ListingId = e.ListingId AND h.Date = e.HighDate AND h.Price = e.HighPrice)
            """));
    }

    [Fact]
    public void NoStoredPointIsBelowTheLowOrAboveTheHigh_AndTheCurrentPriceIsWithinThem()
    {
        Assert.Equal(0, Count("""
            SELECT COUNT(*) FROM PriceHistory h JOIN PriceExtremes e ON e.ListingId = h.ListingId
            WHERE h.Price < e.LowPrice OR h.Price > e.HighPrice
            """));
        Assert.Equal(0, Count("""
            SELECT COUNT(*) FROM Listings l JOIN PriceExtremes e ON e.ListingId = l.Id
            WHERE l.CurrentPrice < e.LowPrice OR l.CurrentPrice > e.HighPrice
            """));
    }

    [Fact]
    public void HistoryIsWeeklyNotDaily()
    {
        var maxPoints = Count("SELECT MAX(c) FROM (SELECT COUNT(*) AS c FROM PriceHistory GROUP BY ListingId)");

        Assert.InRange(maxPoints, 1, 62); // 400 days of daily data would be 400 points
    }

    [Fact]
    public void HistoryPointsAreSpacedAWeekApart_ExceptTheExtremeDays()
    {
        var history = new PriceHistoryService(_db);
        var listingId = Count("SELECT MIN(Id) FROM Listings");
        var extremes = history.GetExtremes(listingId)!;

        var points = history.GetPoints(listingId, DateOnly.MinValue, DateOnly.MaxValue);
        var regular = points.Where(p => p.Date != extremes.LowDate && p.Date != extremes.HighDate).ToList();

        for (var i = 1; i < regular.Count; i++)
            Assert.Equal(0, (regular[i].Date.DayNumber - regular[i - 1].Date.DayNumber) % HistoryCompactor.SampleIntervalDays);
    }

    [Fact]
    public void TheNinetyDayAverageWorksOnWeeklyData()
    {
        var history = new PriceHistoryService(_db);
        var listingId = Count("SELECT MIN(Id) FROM Listings");

        var average = history.GetAverage(listingId, 90, DateOnly.FromDateTime(DateTime.Today));

        Assert.NotNull(average);
        Assert.True(average > 0);
    }

    [Fact]
    public void TheDemoCatalogIsSearchable()
    {
        var search = new ProductSearchService(_db);

        Assert.Equal(3, search.Search("headphones").TotalMatches);
        Assert.Equal(3, search.Search("wireless head").TotalMatches);
        Assert.Equal(3, search.Search("monitor").TotalMatches);
        Assert.Equal(1, search.Search("apex wireless").TotalMatches);
    }

    [Fact]
    public void SeedingTwiceStillCreatesNoDuplicates()
    {
        var tables = new[] { "Products", "Listings", "PriceHistory", "PriceExtremes", "Brands", "Marketplaces" };
        var before = tables.Select(t => Count($"SELECT COUNT(*) FROM {t}")).ToArray();

        new Seeder(_db).Run();

        Assert.Equal(before, tables.Select(t => Count($"SELECT COUNT(*) FROM {t}")).ToArray());
        Assert.Equal(3, new ProductSearchService(_db).Search("headphones").TotalMatches);
    }

    [Fact]
    public void Initialize_RecreatesAMissingExtremesTable()
    {
        using (var conn = _db.Open())
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "DROP TABLE PriceExtremes";
            cmd.ExecuteNonQuery();
        }

        _db.Initialize();

        Assert.Equal(0, Count("SELECT COUNT(*) FROM PriceExtremes"));
        new PriceHistoryService(_db).RecordPrice(Count("SELECT MIN(Id) FROM Listings"), new DateOnly(2026, 10, 12), 25);
        Assert.Equal(1, Count("SELECT COUNT(*) FROM PriceExtremes"));
    }
}
