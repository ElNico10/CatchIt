using CatchIt.Core.Data;
using CatchIt.Core.Pricing;
using Microsoft.Data.Sqlite;

namespace CatchIt.Tests;

/// <summary>
/// Tests for <see cref="PriceHistoryService"/>: reading history and extremes, the 90-day
/// average, and keeping the compact shape (weekly points plus extremes) as new prices arrive.
/// Each test gets its own throwaway database file.
/// </summary>
public class PriceHistoryServiceTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 10, 12);

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"catchit-test-{Guid.NewGuid():N}.db");
    private readonly Database _db;
    private readonly PriceHistoryService _history;
    private readonly long _listing;

    public PriceHistoryServiceTests()
    {
        _db = new Database(_path);
        _db.Initialize();
        _history = new PriceHistoryService(_db);
        _listing = TestCatalog.AddListing(_db, TestCatalog.AddProduct(_db, "Test Widget"));
    }

    public void Dispose()
    {
        // SQLite keeps pooled connections open, which would stop the file being deleted.
        SqliteConnection.ClearAllPools();
        if (File.Exists(_path)) File.Delete(_path);
    }

    private long PointCount() => TestCatalog.Count(_db, $"SELECT COUNT(*) FROM PriceHistory WHERE ListingId = {_listing}");

    private double CurrentPrice()
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT CurrentPrice FROM Listings WHERE Id = {_listing}";
        return Convert.ToDouble(cmd.ExecuteScalar());
    }

    // ---- Extremes ----

    [Fact]
    public void GetExtremes_WhenNothingRecorded_ReturnsNull()
    {
        Assert.Null(_history.GetExtremes(_listing));
    }

    [Fact]
    public void RecordPrice_FirstPrice_CreatesExtremesAndAPoint()
    {
        _history.RecordPrice(_listing, Today, 40);

        var extremes = _history.GetExtremes(_listing)!;
        Assert.Equal(40, extremes.LowPrice);
        Assert.Equal(Today, extremes.LowDate);
        Assert.Equal(40, extremes.HighPrice);
        Assert.Equal(Today, extremes.HighDate);
        Assert.Equal(1, PointCount());
    }

    [Fact]
    public void RecordPrice_NewLow_UpdatesTheLowWithItsDate_AndKeepsTheHigh()
    {
        _history.RecordPrice(_listing, Today, 40);

        _history.RecordPrice(_listing, Today.AddDays(1), 30);

        var extremes = _history.GetExtremes(_listing)!;
        Assert.Equal(30, extremes.LowPrice);
        Assert.Equal(Today.AddDays(1), extremes.LowDate);
        Assert.Equal(40, extremes.HighPrice);
        Assert.Equal(Today, extremes.HighDate);
    }

    [Fact]
    public void RecordPrice_NewHigh_UpdatesTheHighWithItsDate_AndKeepsTheLow()
    {
        _history.RecordPrice(_listing, Today, 40);

        _history.RecordPrice(_listing, Today.AddDays(2), 55);

        var extremes = _history.GetExtremes(_listing)!;
        Assert.Equal(55, extremes.HighPrice);
        Assert.Equal(Today.AddDays(2), extremes.HighDate);
        Assert.Equal(40, extremes.LowPrice);
        Assert.Equal(Today, extremes.LowDate);
    }

    [Fact]
    public void RecordPrice_EqualToAnExtreme_KeepsTheEarliestDate()
    {
        _history.RecordPrice(_listing, Today, 40);
        _history.RecordPrice(_listing, Today.AddDays(1), 30);

        _history.RecordPrice(_listing, Today.AddDays(3), 30);

        Assert.Equal(Today.AddDays(1), _history.GetExtremes(_listing)!.LowDate);
    }

    [Fact]
    public void RecordPrice_NewExtreme_IsStoredAsAPointEvenInsideTheWeeklyWindow()
    {
        _history.RecordPrice(_listing, Today, 40);              // first point
        _history.RecordPrice(_listing, Today.AddDays(2), 25);   // two days later, but a new low

        // The dip would be invisible on a chart without its own point.
        Assert.Equal(2, PointCount());
        Assert.Contains(_history.GetPoints(_listing, Today, Today.AddDays(5)), p => p.Date == Today.AddDays(2) && p.Price == 25);
    }

    // ---- Weekly sampling ----

    [Fact]
    public void RecordPrice_OrdinaryPriceWithinAWeek_UpdatesCurrentPriceButStoresNoPoint()
    {
        _history.RecordPrice(_listing, Today, 40);
        _history.RecordPrice(_listing, Today.AddDays(1), 30);   // new low, stored
        _history.RecordPrice(_listing, Today.AddDays(2), 50);   // new high, stored
        Assert.Equal(3, PointCount());

        _history.RecordPrice(_listing, Today.AddDays(3), 40.5); // between the low and the high, and a point exists this week

        Assert.Equal(40.5, CurrentPrice());
        Assert.Equal(3, PointCount());
    }

    [Fact]
    public void RecordPrice_SixDaysAfterAPoint_IsStillInTheSameWeek()
    {
        _history.RecordPrice(_listing, Today, 40);
        _history.RecordPrice(_listing, Today.AddDays(1), 30);   // new low, stored
        _history.RecordPrice(_listing, Today.AddDays(2), 45);   // new high, stored

        var before = PointCount();
        _history.RecordPrice(_listing, Today.AddDays(6), 35);    // between low and high, 6 days after the first point

        Assert.Equal(before, PointCount());
    }

    [Fact]
    public void RecordPrice_SevenDaysAfterTheLastPoint_StoresANewWeeklyPoint()
    {
        _history.RecordPrice(_listing, Today, 40);
        _history.RecordPrice(_listing, Today.AddDays(1), 30);   // new low, stored
        _history.RecordPrice(_listing, Today.AddDays(2), 50);   // new high, stored (last stored point: day 2)

        _history.RecordPrice(_listing, Today.AddDays(9), 35);    // exactly 7 days after day 2, mid-range

        Assert.Contains(_history.GetPoints(_listing, Today, Today.AddDays(20)), p => p.Date == Today.AddDays(9));
    }

    [Fact]
    public void RecordPrice_SameDayAgain_ReplacesThePointsPrice()
    {
        _history.RecordPrice(_listing, Today, 40);

        _history.RecordPrice(_listing, Today, 38);

        var points = _history.GetPoints(_listing, Today, Today);
        Assert.Single(points);
        Assert.Equal(38, points[0].Price);
        Assert.Equal(38, _history.GetExtremes(_listing)!.LowPrice);
    }

    [Fact]
    public void RecordPrice_OverAYear_StaysCompact()
    {
        var rng = new Random(11);
        for (var d = 0; d < 365; d++)
            _history.RecordPrice(_listing, Today.AddDays(-365 + d), 100 + Math.Round(rng.NextDouble() * 6, 2));

        // About one point a week, plus whatever extra days set a new low or high along the way.
        Assert.InRange(PointCount(), 52, 90);
    }

    // ---- Validation ----

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void RecordPrice_InvalidPrice_ThrowsAndChangesNothing(double price)
    {
        _history.RecordPrice(_listing, Today, 40);

        Assert.Throws<ArgumentOutOfRangeException>(() => _history.RecordPrice(_listing, Today.AddDays(1), price));

        Assert.Equal(40, CurrentPrice());
        Assert.Equal(40, _history.GetExtremes(_listing)!.LowPrice);
    }

    [Fact]
    public void RecordPrice_UnknownListing_ThrowsAndCreatesNothing()
    {
        Assert.Throws<ArgumentException>(() => _history.RecordPrice(99999, Today, 40));

        Assert.Equal(0, TestCatalog.Count(_db, "SELECT COUNT(*) FROM PriceExtremes WHERE ListingId = 99999"));
    }

    // ---- Reading points ----

    [Fact]
    public void GetPoints_ReturnsTheRequestedRangeOldestFirst_Inclusive()
    {
        foreach (var offset in new[] { -30, -20, -10, 0 })
            TestCatalog.AddPoint(_db, _listing, Today.AddDays(offset), 50 + offset);

        var points = _history.GetPoints(_listing, Today.AddDays(-20), Today.AddDays(-10));

        Assert.Equal(new[] { Today.AddDays(-20), Today.AddDays(-10) }, points.Select(p => p.Date));
        Assert.Equal(new[] { 30.0, 40.0 }, points.Select(p => p.Price));
    }

    [Fact]
    public void GetPoints_WithNoHistory_ReturnsAnEmptyList()
    {
        Assert.Empty(_history.GetPoints(_listing, Today.AddDays(-100), Today));
    }

    // ---- 90-day average (story #8) ----

    [Fact]
    public void GetAverage_UsesPointsInTheWindowBeforeAsOf_AndNotAsOfItself()
    {
        TestCatalog.AddPoint(_db, _listing, Today.AddDays(-91), 1000); // too old, outside the window
        TestCatalog.AddPoint(_db, _listing, Today.AddDays(-90), 100);  // first day of the window, included
        TestCatalog.AddPoint(_db, _listing, Today.AddDays(-45), 80);
        TestCatalog.AddPoint(_db, _listing, Today.AddDays(-7), 60);
        TestCatalog.AddPoint(_db, _listing, Today, 5);                 // the day itself, excluded

        var average = _history.GetAverage(_listing, 90, Today);

        Assert.Equal(80.0, average!.Value, 6);
    }

    [Fact]
    public void GetAverage_WithFewerThanTwoPoints_ReturnsNull()
    {
        Assert.Null(_history.GetAverage(_listing, 90, Today));

        TestCatalog.AddPoint(_db, _listing, Today.AddDays(-3), 50);
        Assert.Null(_history.GetAverage(_listing, 90, Today));
    }

    [Fact]
    public void GetAverage_WithTwoPoints_ReturnsTheirAverage()
    {
        TestCatalog.AddPoint(_db, _listing, Today.AddDays(-14), 50);
        TestCatalog.AddPoint(_db, _listing, Today.AddDays(-7), 70);

        Assert.Equal(60.0, _history.GetAverage(_listing, 90, Today)!.Value, 6);
    }

    [Fact]
    public void GetAverage_IgnoresOtherListings()
    {
        var other = TestCatalog.AddListing(_db, TestCatalog.AddProduct(_db, "Other Widget"), "OtherMarket");
        TestCatalog.AddPoint(_db, _listing, Today.AddDays(-14), 50);
        TestCatalog.AddPoint(_db, _listing, Today.AddDays(-7), 70);
        TestCatalog.AddPoint(_db, other, Today.AddDays(-14), 1000);
        TestCatalog.AddPoint(_db, other, Today.AddDays(-7), 1000);

        Assert.Equal(60.0, _history.GetAverage(_listing, 90, Today)!.Value, 6);
    }
}
