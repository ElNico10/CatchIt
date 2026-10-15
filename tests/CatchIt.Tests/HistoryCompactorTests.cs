using CatchIt.Core.Pricing;

namespace CatchIt.Tests;

/// <summary>
/// Tests for <see cref="HistoryCompactor"/>: weekly samples, plus the absolute low and high
/// days, which must survive even when they fall between two weekly samples.
/// </summary>
public class HistoryCompactorTests
{
    private static readonly DateOnly Start = new(2026, 1, 1);

    /// <summary>Builds a daily series starting on <see cref="Start"/>, with the price of each day chosen by a function.</summary>
    private static List<PricePoint> Series(int days, Func<int, double> priceOfDay) =>
        Enumerable.Range(0, days).Select(i => new PricePoint(Start.AddDays(i), priceOfDay(i))).ToList();

    [Fact]
    public void Compact_KeepsOnePointAWeek_CountedBackFromTheNewestDay()
    {
        // 30 days: day 0 to day 29. Weekly samples back from day 29 are 29, 22, 15, 8 and 1.
        // The flat price makes day 0 both the low and the high, so it is kept as well.
        var result = HistoryCompactor.Compact(Series(30, _ => 10));

        var days = result.Points.Select(p => p.Date.DayNumber - Start.DayNumber).ToList();
        Assert.Equal(new[] { 0, 1, 8, 15, 22, 29 }, days);
    }

    [Fact]
    public void Compact_AlwaysKeepsTheNewestPoint()
    {
        var series = Series(100, i => 10 + i % 3);

        var result = HistoryCompactor.Compact(series);

        Assert.Equal(series[^1], result.Points[^1]);
    }

    [Fact]
    public void Compact_KeepsAHolidaySaleThatFallsBetweenWeeklySamples()
    {
        // Day 10 is not a weekly sample (weekly days here are 29, 22, 15, 8, 1), but it is the cheapest day.
        var series = Series(30, i => i == 10 ? 5 : 20);

        var result = HistoryCompactor.Compact(series);

        Assert.Equal(new PricePoint(Start.AddDays(10), 5), result.Low);
        Assert.Contains(result.Points, p => p.Date == Start.AddDays(10) && p.Price == 5);
    }

    [Fact]
    public void Compact_KeepsAPeakThatFallsBetweenWeeklySamples()
    {
        var series = Series(30, i => i == 12 ? 99 : 20);

        var result = HistoryCompactor.Compact(series);

        Assert.Equal(new PricePoint(Start.AddDays(12), 99), result.High);
        Assert.Contains(result.Points, p => p.Date == Start.AddDays(12) && p.Price == 99);
    }

    [Fact]
    public void Compact_WhenSeveralDaysShareTheLowestPrice_ReportsTheEarliest()
    {
        var series = Series(30, i => i is 4 or 9 or 20 ? 5 : 20);

        var result = HistoryCompactor.Compact(series);

        Assert.Equal(Start.AddDays(4), result.Low.Date);
    }

    [Fact]
    public void Compact_WhenSeveralDaysShareTheHighestPrice_ReportsTheEarliest()
    {
        var series = Series(30, i => i is 6 or 13 ? 80 : 20);

        var result = HistoryCompactor.Compact(series);

        Assert.Equal(Start.AddDays(6), result.High.Date);
    }

    [Fact]
    public void Compact_DoesNotDependOnTheOrderOfTheInput()
    {
        var series = Series(60, i => 10 + (i * 7) % 13);
        var shuffled = series.OrderBy(p => (p.Date.DayNumber * 31) % 17).ThenBy(p => p.Date).ToList();

        var a = HistoryCompactor.Compact(series);
        var b = HistoryCompactor.Compact(shuffled);

        Assert.Equal(a.Points, b.Points);
        Assert.Equal(a.Low, b.Low);
        Assert.Equal(a.High, b.High);
    }

    [Fact]
    public void Compact_ReturnsPointsOldestFirstWithNoDuplicateDates()
    {
        // The low and high both land on weekly samples here, so they must not be stored twice.
        var series = Series(50, i => i == 1 ? 1 : i == 29 ? 500 : 20);

        var result = HistoryCompactor.Compact(series);

        var dates = result.Points.Select(p => p.Date).ToList();
        Assert.Equal(dates.OrderBy(d => d).ToList(), dates);
        Assert.Equal(dates.Distinct().Count(), dates.Count);
    }

    [Fact]
    public void Compact_SinglePoint_IsItsOwnLowAndHigh()
    {
        var point = new PricePoint(Start, 42);

        var result = HistoryCompactor.Compact(new[] { point });

        Assert.Single(result.Points);
        Assert.Equal(point, result.Low);
        Assert.Equal(point, result.High);
    }

    [Fact]
    public void Compact_EmptySeries_Throws()
    {
        Assert.Throws<ArgumentException>(() => HistoryCompactor.Compact(Array.Empty<PricePoint>()));
    }

    [Fact]
    public void Compact_AYearOfDailyPrices_ShrinksByAboutSevenTimes()
    {
        var series = Series(401, i => 100 + Math.Sin(i / 9.0) * 8);

        var result = HistoryCompactor.Compact(series);

        // 401 days is 58 weekly samples (days 0 to 400 in steps of 7), plus at most the low and the high.
        Assert.InRange(result.Points.Count, 57, 60);
    }

    [Fact]
    public void Compact_ExtremesAreFoundInTheFullSeries_NotJustTheStoredPoints()
    {
        // The true low is a one-day dip. The stored weekly points alone would never show it.
        var series = Series(100, i => i == 33 ? 1 : 50);

        var result = HistoryCompactor.Compact(series);

        Assert.Equal(1, result.Low.Price);
        Assert.Equal(Start.AddDays(33), result.Low.Date);
    }
}
