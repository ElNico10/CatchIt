namespace CatchIt.Core.Pricing;

/// <summary>
/// The named yearly sale periods used to explain a seasonal price drop ("around Black Friday").
/// </summary>
/// <remarks>
/// The periods match the sale calendars of the simulated marketplaces (see <see cref="PriceHistoryGenerator"/>).
/// With real price history they would still be sensible names, but should be checked against it.
/// Christmas itself is deliberately not listed: in the simulated data December prices rise for holiday demand,
/// and the only December cut is the post-Christmas clearance.
/// </remarks>
public static class SaleEvents
{
    // From and to are month*100 + day, inclusive, and the same every year. An event that wraps past December
    // would need splitting in two; none does.
    private static readonly (int From, int To, string Name)[] Events =
    {
        (101, 114, "New Year"),
        (315, 321, "the spring sale"),
        (708, 719, "the summer sale"),
        (801, 831, "back to school"),
        (1121, 1203, "Black Friday / Cyber Monday"),
        (1226, 1231, "post-Christmas clearance"),
    };

    /// <summary>Finds the named sale period a date falls in.</summary>
    /// <param name="date">The day to look up; the year does not matter.</param>
    /// <returns>The period's name ready for a sentence ("Black Friday / Cyber Monday"), or <c>null</c> if the day is in none.</returns>
    public static string? NameFor(DateOnly date)
    {
        var monthDay = date.Month * 100 + date.Day;
        foreach (var (from, to, name) in Events)
        {
            // Black Friday crosses from November into December, so compare in two parts when it does.
            var inside = from <= to ? monthDay >= from && monthDay <= to : monthDay >= from || monthDay <= to;
            if (inside) return name;
        }
        return null;
    }
}

/// <summary>A price that was clearly lower at this time last year (see <see cref="SeasonalDrops.Find"/>).</summary>
/// <param name="Percent">How much cheaper it was than today's price, 0 to 100.</param>
/// <param name="LowDate">The day the lowest price in that period fell on.</param>
/// <param name="LowPrice">The lowest price in that period.</param>
/// <param name="EventName">The named sale period the low fell in, or <c>null</c> if it fell outside all of them.</param>
public record SeasonalDrop(double Percent, DateOnly LowDate, double LowPrice, string? EventName);

/// <summary>Spots listings whose price dipped around this time of year last year.</summary>
public static class SeasonalDrops
{
    /// <summary>The smallest drop worth mentioning: 10% below today's price.</summary>
    public const double MinDrop = 0.10;

    /// <summary>How many days ahead of today's date, last year, the look-ahead window starts (a year back).</summary>
    public const int WindowStartDaysAgo = 365;

    /// <summary>How long the look-ahead window is, in days (about a month).</summary>
    public const int WindowDays = 30;

    /// <summary>The first day of last year's look-ahead window.</summary>
    /// <param name="asOf">Today.</param>
    public static DateOnly WindowStart(DateOnly asOf) => asOf.AddDays(-WindowStartDaysAgo);

    /// <summary>The last day of last year's look-ahead window.</summary>
    /// <param name="asOf">Today.</param>
    public static DateOnly WindowEnd(DateOnly asOf) => asOf.AddDays(-WindowStartDaysAgo + WindowDays);

    /// <summary>Finds the lowest price in last year's window and says how far below today's price it was.</summary>
    /// <param name="windowPoints">The listing's stored points from <see cref="WindowStart"/> to <see cref="WindowEnd"/>.</param>
    /// <param name="currentPrice">The listing's price today.</param>
    /// <returns>The drop, or <c>null</c> when there are no points or the low was less than <see cref="MinDrop"/> below today's price.</returns>
    public static SeasonalDrop? Find(IReadOnlyList<PricePoint> windowPoints, double currentPrice)
    {
        if (windowPoints.Count == 0 || !(currentPrice > 0)) return null;

        // The first of equal lows wins, so the same data always gives the same date.
        var low = windowPoints.MinBy(p => p.Price)!;
        var drop = 1 - low.Price / currentPrice;
        if (drop < MinDrop - 1e-9) return null;

        return new SeasonalDrop(Math.Round(drop * 100, 1), low.Date, low.Price, SaleEvents.NameFor(low.Date));
    }
}
