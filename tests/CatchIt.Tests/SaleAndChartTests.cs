using CatchIt.Core.Data;
using CatchIt.Core.Pricing;
using CatchIt.Core.Search;
using Microsoft.Data.Sqlite;

namespace CatchIt.Tests;

/// <summary>
/// Tests for the On Sale rule, the named sale periods, seasonal drops and the chart calculations.
/// </summary>
public class SaleAndChartTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 11, 3);

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"catchit-test-{Guid.NewGuid():N}.db");
    private readonly Database _db;

    public SaleAndChartTests()
    {
        _db = new Database(_path);
        _db.Initialize();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_path)) File.Delete(_path);
    }

    private static PricePoint P(DateOnly date, double price) => new(date, price);

    // ---- The On Sale rule (story #8) ----

    [Theory]
    [InlineData(100, 90, true)]       // exactly 10% below: on sale
    [InlineData(100, 89.99, true)]
    [InlineData(100, 90.01, false)]   // just short of 10%
    [InlineData(100, 100, false)]     // equal to the average is not on sale
    [InlineData(100, 120, false)]     // above the average
    public void IsOnSale_NeedsAtLeastTenPercentBelowTheAverage(double average, double price, bool expected) =>
        Assert.Equal(expected, SaleCheck.IsOnSale(price, average));

    [Fact]
    public void IsOnSale_NeverFlagsWithoutAnAverage() => Assert.False(SaleCheck.IsOnSale(1.0, null));

    /// <summary>Adds a listing with one weekly point at <paramref name="historyPrice"/> for the ten weeks before today.</summary>
    private long AddListing(string name, double? currentPrice, double historyPrice, int weeks = 10)
    {
        var listing = TestCatalog.AddListing(_db, TestCatalog.AddProduct(_db, name, keywords: "salecheck"), currentPrice: currentPrice);
        for (var i = 1; i <= weeks; i++) TestCatalog.AddPoint(_db, listing, Today.AddDays(-7 * i), historyPrice);
        return listing;
    }

    [Fact]
    public void SearchResults_FlagOnlyListingsClearlyBelowTheirAverage_InEverySearchKind()
    {
        var cheap = AddListing("Deal Gadget", 85, 100);          // 15% below its average
        var equal = AddListing("Equal Gadget", 100, 100);         // equal
        var near = AddListing("Near Gadget", 95, 100);            // only 5% below
        var lone = TestCatalog.AddListing(_db, TestCatalog.AddProduct(_db, "Lone Gadget", keywords: "salecheck"), currentPrice: 10);
        TestCatalog.AddPoint(_db, lone, Today.AddDays(-7), 100); // one point: not enough history for an average
        var unpriced = AddListing("Unpriced Gadget", null, 100);

        var service = new ListingSearchService(_db, Today);
        var pages = new[]
        {
            service.Search("salecheck"),
            service.QuickSearch("salecheck"),
            service.Search(null),        // browsing the whole catalog
        };

        foreach (var page in pages)
        {
            var flagged = page.Hits.Where(h => h.OnSale).Select(h => h.ListingId).ToList();
            Assert.Equal(new[] { cheap }, flagged);
            Assert.Equal(5, page.Hits.Count);
        }
        Assert.DoesNotContain(equal, pages[0].Hits.Where(h => h.OnSale).Select(h => h.ListingId));
        Assert.DoesNotContain(near, pages[0].Hits.Where(h => h.OnSale).Select(h => h.ListingId));
        Assert.DoesNotContain(lone, pages[0].Hits.Where(h => h.OnSale).Select(h => h.ListingId));
        Assert.DoesNotContain(unpriced, pages[0].Hits.Where(h => h.OnSale).Select(h => h.ListingId));
    }

    [Fact]
    public void SearchResultFlags_MatchTheSingleListingCalculation_ForEveryListingInTheSeededCatalog()
    {
        new Seeder(_db).Run();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var history = new PriceHistoryService(_db);

        var page = new ListingSearchService(_db).Search(null, limit: 500);

        Assert.True(page.Hits.Count > 20);
        foreach (var hit in page.Hits)
        {
            var expected = hit.CurrentPrice is { } price && SaleCheck.IsOnSale(price, history.GetAverage(hit.ListingId, SaleCheck.AverageDays, today));
            Assert.Equal(expected, hit.OnSale);
        }
    }

    // ---- Named sale periods ----

    [Theory]
    [InlineData(2025, 11, 21, "Black Friday / Cyber Monday")]
    [InlineData(2025, 11, 28, "Black Friday / Cyber Monday")]
    [InlineData(2025, 12, 3, "Black Friday / Cyber Monday")]   // the period runs into December
    [InlineData(2025, 12, 26, "post-Christmas clearance")]
    [InlineData(2026, 1, 1, "New Year")]
    [InlineData(2026, 7, 10, "the summer sale")]
    [InlineData(2026, 8, 15, "back to school")]
    [InlineData(2026, 3, 18, "the spring sale")]
    public void SaleEvents_NamesTheSalePeriodADateFallsIn(int year, int month, int day, string expected) =>
        Assert.Equal(expected, SaleEvents.NameFor(new DateOnly(year, month, day)));

    [Theory]
    [InlineData(2025, 11, 20)]
    [InlineData(2025, 12, 4)]
    [InlineData(2025, 12, 24)]   // Christmas itself is not a sale period
    [InlineData(2026, 2, 14)]
    public void SaleEvents_ReturnsNullOutsideEveryPeriod(int year, int month, int day) =>
        Assert.Null(SaleEvents.NameFor(new DateOnly(year, month, day)));

    // ---- Seasonal drops ----

    [Fact]
    public void SeasonalDrops_WindowIsTheNextThirtyDaysOfLastYear()
    {
        Assert.Equal(new DateOnly(2025, 11, 3), SeasonalDrops.WindowStart(Today));
        Assert.Equal(new DateOnly(2025, 12, 3), SeasonalDrops.WindowEnd(Today));
    }

    [Fact]
    public void SeasonalDrops_Find_ReportsTheBiggestDipWithItsDateAndEvent()
    {
        var window = new[]
        {
            P(new DateOnly(2025, 11, 5), 95),
            P(new DateOnly(2025, 11, 28), 60),
            P(new DateOnly(2025, 12, 2), 80),
        };

        var drop = SeasonalDrops.Find(window, 100)!;

        Assert.Equal(40, drop.Percent);
        Assert.Equal(60, drop.LowPrice);
        Assert.Equal(new DateOnly(2025, 11, 28), drop.LowDate);
        Assert.Equal("Black Friday / Cyber Monday", drop.EventName);
    }

    [Fact]
    public void SeasonalDrops_Find_NamesNoEventWhenTheLowIsOutsideAllPeriods()
    {
        var drop = SeasonalDrops.Find(new[] { P(new DateOnly(2025, 11, 10), 70) }, 100)!;

        Assert.Null(drop.EventName);
    }

    [Fact]
    public void SeasonalDrops_Find_RequiresAtLeastTenPercent()
    {
        Assert.NotNull(SeasonalDrops.Find(new[] { P(new DateOnly(2025, 11, 28), 90) }, 100));     // exactly 10%
        Assert.Null(SeasonalDrops.Find(new[] { P(new DateOnly(2025, 11, 28), 90.5) }, 100));      // just under
        Assert.Null(SeasonalDrops.Find(new[] { P(new DateOnly(2025, 11, 28), 120) }, 100));       // it was dearer then
    }

    [Fact]
    public void SeasonalDrops_Find_HandlesNoDataAndBadPrices()
    {
        Assert.Null(SeasonalDrops.Find(Array.Empty<PricePoint>(), 100));
        Assert.Null(SeasonalDrops.Find(new[] { P(new DateOnly(2025, 11, 28), 50) }, 0));
    }

    // ---- Chart ranges ----

    private static List<PricePoint> Weekly(DateOnly end, int weeks) =>
        Enumerable.Range(0, weeks).Select(i => P(end.AddDays(-7 * (weeks - 1 - i)), 50 + i)).ToList();

    [Fact]
    public void Filter_KeepsOnlyThePointsInsideTheRange()
    {
        var points = Weekly(Today, 70); // about 16 months

        Assert.Equal(points.Count, PriceChartMath.Filter(points, ChartRange.All, Today).Count);
        Assert.All(PriceChartMath.Filter(points, ChartRange.ThreeMonths, Today), p => Assert.True(p.Date >= Today.AddMonths(-3)));
        Assert.All(PriceChartMath.Filter(points, ChartRange.TwelveMonths, Today), p => Assert.True(p.Date >= Today.AddMonths(-12)));
        Assert.True(PriceChartMath.Filter(points, ChartRange.ThreeMonths, Today).Count
                    < PriceChartMath.Filter(points, ChartRange.SixMonths, Today).Count);
        Assert.True(PriceChartMath.Filter(points, ChartRange.SixMonths, Today).Count
                    < PriceChartMath.Filter(points, ChartRange.TwelveMonths, Today).Count);
    }

    [Fact]
    public void HasEnoughHistory_NeedsTwoPointsAWeekApart()
    {
        Assert.False(PriceChartMath.HasEnoughHistory(Array.Empty<PricePoint>()));
        Assert.False(PriceChartMath.HasEnoughHistory(new[] { P(Today, 10) }));
        Assert.False(PriceChartMath.HasEnoughHistory(new[] { P(Today.AddDays(-6), 10), P(Today, 11) }));   // under a week
        Assert.True(PriceChartMath.HasEnoughHistory(new[] { P(Today.AddDays(-7), 10), P(Today, 11) }));
    }

    [Fact]
    public void DefaultRange_PrefersTwelveMonthsThenFallsBackToWhatTheHistoryCovers()
    {
        Assert.Equal(ChartRange.TwelveMonths, PriceChartMath.DefaultRange(Weekly(Today, 70), Today));
        Assert.Equal(ChartRange.SixMonths, PriceChartMath.DefaultRange(Weekly(Today, 30), Today));      // about 7 months
        Assert.Equal(ChartRange.ThreeMonths, PriceChartMath.DefaultRange(Weekly(Today, 20), Today));    // about 5 months
        Assert.Equal(ChartRange.All, PriceChartMath.DefaultRange(Weekly(Today, 8), Today));             // under 3 months
        // A listing with a single week of history does not reach back to any month-based range, so it shows everything.
        Assert.Equal(ChartRange.All, PriceChartMath.DefaultRange(new[] { P(Today.AddDays(-7), 10), P(Today, 11) }, Today));
        Assert.Equal(ChartRange.All, PriceChartMath.DefaultRange(Array.Empty<PricePoint>(), Today));
    }

    // ---- Axis labels ----

    [Fact]
    public void NiceTicks_CoverTheDataWithEvenRoundSteps()
    {
        var ticks = PriceChartMath.NiceTicks(38.2, 52.9);

        Assert.True(ticks[0] <= 38.2);
        Assert.True(ticks[^1] >= 52.9);
        var step = ticks[1] - ticks[0];
        for (var i = 1; i < ticks.Count; i++) Assert.Equal(step, ticks[i] - ticks[i - 1], 6);
    }

    [Fact]
    public void NiceTicks_HandleAFlatPrice() => Assert.True(PriceChartMath.NiceTicks(25, 25).Count >= 2);

    [Fact]
    public void NiceTicks_AlwaysCoverTheRange_WithAReasonableNumberOfLabels()
    {
        var rng = new Random(11);
        for (var i = 0; i < 2000; i++)
        {
            var min = Math.Round(rng.NextDouble() * 500 + 1, 2);
            var max = min + rng.NextDouble() * Math.Pow(10, rng.Next(0, 4));
            var ticks = PriceChartMath.NiceTicks(min, max);

            Assert.True(ticks[0] <= min + 1e-9, $"{min}-{max}");
            Assert.True(ticks[^1] >= max - 1e-9, $"{min}-{max}");
            Assert.InRange(ticks.Count, 3, 12);
        }
    }
}
