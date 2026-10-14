using CatchIt.Core.Pricing;

namespace CatchIt.Tests;

/// <summary>
/// Tests for <see cref="PriceHistoryGenerator"/>, the simulated history used for demo data.
/// The seasonal tests check statistics over many generated series with a fixed random seed,
/// so they are repeatable.
/// </summary>
public class PriceHistoryGeneratorTests
{
    private static readonly DateOnly End = new(2026, 10, 12);
    private const int Days = 400;

    private static IReadOnlyList<PricePoint> Make(int seed, int marketplace = 0, double basePrice = 100) =>
        PriceHistoryGenerator.Generate(basePrice, marketplace, End, Days, new Random(seed));

    // ---- Shape ----

    [Fact]
    public void Generate_ProducesOneConsecutivePointPerDayEndingOnTheEndDate()
    {
        var series = Make(1);

        Assert.Equal(Days + 1, series.Count);
        Assert.Equal(End, series[^1].Date);
        Assert.Equal(End.AddDays(-Days), series[0].Date);
        for (var i = 1; i < series.Count; i++)
            Assert.Equal(series[i - 1].Date.AddDays(1), series[i].Date);
    }

    [Fact]
    public void Generate_PricesAreFiniteAndAtLeastOneDollar()
    {
        foreach (var marketplace in Enumerable.Range(0, 4))
        foreach (var basePrice in new[] { 1.5, 18, 420 })
            Assert.All(Make(marketplace, marketplace, basePrice), p =>
            {
                Assert.True(p.Price >= 1.0);
                Assert.False(double.IsNaN(p.Price) || double.IsInfinity(p.Price));
            });
    }

    [Fact]
    public void Generate_PricesAreRoundedToCents()
    {
        Assert.All(Make(3), p => Assert.Equal(Math.Round(p.Price, 2), p.Price));
    }

    [Fact]
    public void Generate_IsRepeatableForTheSameSeed()
    {
        Assert.Equal(Make(7, 2), Make(7, 2));
    }

    [Fact]
    public void Generate_DifferentSeedsGiveDifferentHistories()
    {
        Assert.NotEqual(Make(7), Make(8));
    }

    [Fact]
    public void Generate_DifferentMarketplacesFollowDifferentSaleCalendars()
    {
        // Same randomness, different marketplace profile: the sale windows land on different days.
        Assert.NotEqual(Make(5, 0), Make(5, 1));
    }

    [Fact]
    public void Generate_MarketplaceIndexBeyondTheFourProfiles_StillWorks()
    {
        Assert.Equal(Days + 1, Make(2, 11).Count);
        Assert.Equal(Days + 1, Make(2, -3).Count);
    }

    [Fact]
    public void Generate_ZeroDays_GivesASingleTodayPoint()
    {
        var series = PriceHistoryGenerator.Generate(50, 0, End, 0, new Random(1));

        Assert.Single(series);
        Assert.Equal(End, series[0].Date);
    }

    [Fact]
    public void Generate_InvalidInput_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PriceHistoryGenerator.Generate(0, 0, End, 10, new Random(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => PriceHistoryGenerator.Generate(-5, 0, End, 10, new Random(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => PriceHistoryGenerator.Generate(50, 0, End, -1, new Random(1)));
    }

    // ---- Seasonality: the point of the model ----

    /// <summary>True for days in the sale seasons built into the marketplace calendars.</summary>
    private static bool InSaleSeason(DateOnly d) =>
        (d.Month == 11 && d.Day >= 20) || (d.Month == 12 && d.Day <= 5)   // the November sales
        || (d.Month == 7 && d.Day >= 5 && d.Day <= 20)                    // summer sale
        || (d.Month == 8)                                                 // back to school
        || (d.Month == 12 && d.Day >= 26) || (d.Month == 1 && d.Day <= 16) // after Christmas, New Year
        || (d.Month == 3 && d.Day >= 15 && d.Day <= 25);                  // spring sale

    private static List<IReadOnlyList<PricePoint>> ManySeries(int count) =>
        Enumerable.Range(0, count).Select(i => Make(1000 + i, i % 4)).ToList();

    [Fact]
    public void Generate_TheCheapestDayUsuallyFallsInASaleSeason()
    {
        var all = ManySeries(300);

        var inSeason = all.Count(s => InSaleSeason(HistoryCompactor.Compact(s).Low.Date));

        // Sale seasons cover about a quarter of the calendar, so chance alone would give about 25%.
        Assert.True(inSeason >= all.Count * 0.6, $"only {inSeason} of {all.Count} lows were in a sale season");
    }

    [Fact]
    public void Generate_TheHighestDayUsuallyFallsInTheDecemberDemandSeason()
    {
        var all = ManySeries(300);

        var inDecember = all.Count(s =>
        {
            var high = HistoryCompactor.Compact(s).High.Date;
            return high.Month == 12 && high.Day <= 24;
        });

        // December 1-24 is about 6% of the calendar, so chance alone would give about 6%.
        Assert.True(inDecember >= all.Count * 0.3, $"only {inDecember} of {all.Count} highs were in December");
    }

    [Fact]
    public void Generate_BlackFridayWeekIsCheaperThanAQuietWeekInOctober()
    {
        var all = ManySeries(300);
        double Average(DateOnly day) => all.Average(s => s.First(p => p.Date == day).Price);

        var blackFriday = Average(new DateOnly(2025, 11, 28));
        var quietOctober = Average(new DateOnly(2025, 10, 15));

        Assert.True(blackFriday < quietOctober * 0.92, $"Black Friday {blackFriday:F2} vs October {quietOctober:F2}");
    }

    [Fact]
    public void Generate_PricesDoNotSwingWildlyFromDayToDay_OutsideSales()
    {
        // Mid-October to mid-November has no scheduled sale, so day-to-day changes stay small
        // (flash sales aside, which are rare and brief): the median daily change is under 1%.
        var changes = new List<double>();
        foreach (var s in ManySeries(100))
        {
            var window = s.Where(p => p.Date >= new DateOnly(2025, 10, 1) && p.Date <= new DateOnly(2025, 11, 10)).ToList();
            for (var i = 1; i < window.Count; i++)
                changes.Add(Math.Abs(window[i].Price - window[i - 1].Price) / window[i - 1].Price);
        }
        changes.Sort();

        Assert.True(changes[changes.Count / 2] < 0.01);
    }
}
