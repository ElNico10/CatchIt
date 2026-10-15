namespace CatchIt.Core.Pricing;

/// <summary>The result of compacting a daily price series: what to store, and the extremes.</summary>
/// <param name="Points">The points to store in the history table, oldest first, one per date.</param>
/// <param name="Low">The lowest price in the original series (its first occurrence).</param>
/// <param name="High">The highest price in the original series (its first occurrence).</param>
public record CompactedHistory(IReadOnlyList<PricePoint> Points, PricePoint Low, PricePoint High);

/// <summary>
/// Shrinks a daily price series into the compact form the database stores: one price a week,
/// plus the absolute lowest and highest days.
/// </summary>
/// <remarks>
/// Prices rarely move much from one day to the next, so a weekly sample is enough for charts
/// and averages and cuts storage by roughly 7 times. The extremes are taken from the full
/// daily series before any days are dropped, so a three-day holiday sale is never lost
/// just because it fell between two weekly samples.
/// </remarks>
public static class HistoryCompactor
{
    /// <summary>Days between the stored samples.</summary>
    public const int SampleIntervalDays = 7;

    /// <summary>Compacts a daily series.</summary>
    /// <param name="dailySeries">
    /// The prices to compact, in any order, normally one per day. Must not be empty.
    /// </param>
    /// <returns>
    /// The weekly samples (counted back from the newest date, which is always kept), plus the
    /// lowest and highest days even if they are not on the weekly schedule. When several days
    /// share the lowest or highest price, the earliest one is reported.
    /// </returns>
    /// <exception cref="ArgumentException">The series is empty.</exception>
    public static CompactedHistory Compact(IReadOnlyList<PricePoint> dailySeries)
    {
        if (dailySeries.Count == 0)
            throw new ArgumentException("The price series must contain at least one point.", nameof(dailySeries));

        var ordered = dailySeries.OrderBy(p => p.Date).ToList();
        var newest = ordered[^1].Date;

        // Strict comparisons mean the first (earliest) day wins a tie.
        var low = ordered[0];
        var high = ordered[0];
        foreach (var point in ordered)
        {
            if (point.Price < low.Price) low = point;
            if (point.Price > high.Price) high = point;
        }

        // Weekly samples are anchored on the newest date so the most recent price is always stored.
        // Keyed by date, so an extreme that is also a weekly sample is only stored once.
        var kept = new SortedDictionary<DateOnly, PricePoint>();
        foreach (var point in ordered)
        {
            if ((newest.DayNumber - point.Date.DayNumber) % SampleIntervalDays == 0)
                kept[point.Date] = point;
        }
        kept[low.Date] = low;
        kept[high.Date] = high;

        return new CompactedHistory(kept.Values.ToList(), low, high);
    }
}
