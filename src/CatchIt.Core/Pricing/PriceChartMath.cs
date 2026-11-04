namespace CatchIt.Core.Pricing;

/// <summary>The time spans offered by the price history chart (user story #7).</summary>
public enum ChartRange
{
    /// <summary>The last 3 months.</summary>
    ThreeMonths,
    /// <summary>The last 6 months.</summary>
    SixMonths,
    /// <summary>The last 12 months.</summary>
    TwelveMonths,
    /// <summary>Everything stored.</summary>
    All,
}

/// <summary>
/// The calculations behind the price chart, kept apart from the drawing code so they can be tested.
/// </summary>
public static class PriceChartMath
{
    /// <summary>The message shown instead of a chart when there is too little history (story #7).</summary>
    public const string NotEnoughHistoryMessage = "Not enough price history available";

    /// <summary>The shortest stretch of history that can be charted: one week.</summary>
    public const int MinSpanDays = 7;

    /// <summary>The first day a range covers, counting back from today.</summary>
    /// <param name="range">The range chosen.</param>
    /// <param name="asOf">Today.</param>
    /// <returns>The first day to include, or <c>null</c> for <see cref="ChartRange.All"/>.</returns>
    public static DateOnly? StartOf(ChartRange range, DateOnly asOf) => range switch
    {
        ChartRange.ThreeMonths => asOf.AddMonths(-3),
        ChartRange.SixMonths => asOf.AddMonths(-6),
        ChartRange.TwelveMonths => asOf.AddMonths(-12),
        _ => null,
    };

    /// <summary>Keeps only the points that fall in a range.</summary>
    /// <param name="points">The stored points, oldest first.</param>
    /// <param name="range">The range chosen.</param>
    /// <param name="asOf">Today.</param>
    public static IReadOnlyList<PricePoint> Filter(IReadOnlyList<PricePoint> points, ChartRange range, DateOnly asOf) =>
        StartOf(range, asOf) is { } start ? points.Where(p => p.Date >= start).ToList() : points;

    /// <summary>Whether a set of points covers enough time to draw a meaningful chart: at least two points, a week apart.</summary>
    /// <param name="points">The points to be drawn, oldest first.</param>
    public static bool HasEnoughHistory(IReadOnlyList<PricePoint> points) =>
        points.Count >= 2 && points[^1].Date.DayNumber - points[0].Date.DayNumber >= MinSpanDays;

    /// <summary>
    /// The longest range the history actually reaches back to, so the chart opens on something useful and the
    /// range buttons mean something. Prefers 12 months (the story's example), then 6, then 3, then everything.
    /// </summary>
    /// <param name="points">All stored points, oldest first.</param>
    /// <param name="asOf">Today.</param>
    public static ChartRange DefaultRange(IReadOnlyList<PricePoint> points, DateOnly asOf)
    {
        if (points.Count == 0) return ChartRange.All;

        // History is stored weekly, so allow a week of slack when asking whether it reaches back to a range's start.
        foreach (var range in new[] { ChartRange.TwelveMonths, ChartRange.SixMonths, ChartRange.ThreeMonths })
            if (points[0].Date <= StartOf(range, asOf)!.Value.AddDays(MinSpanDays) && HasEnoughHistory(Filter(points, range, asOf)))
                return range;
        return ChartRange.All;
    }

    /// <summary>
    /// Picks round numbers (such as 40, 45, 50) to label the price axis, covering the data with a small margin.
    /// </summary>
    /// <param name="min">The lowest price to show.</param>
    /// <param name="max">The highest price to show.</param>
    /// <param name="target">Roughly how many labels are wanted.</param>
    /// <returns>Evenly spaced values from at or below <paramref name="min"/> to at or above <paramref name="max"/>.</returns>
    public static IReadOnlyList<double> NiceTicks(double min, double max, int target = 5)
    {
        if (max <= min) { min -= 1; max += 1; } // a flat price still needs a visible axis

        var rawStep = (max - min) / Math.Max(1, target - 1);
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(rawStep)));
        var fraction = rawStep / magnitude;
        var step = magnitude * (fraction <= 1 ? 1 : fraction <= 2 ? 2 : fraction <= 5 ? 5 : 10);

        // Count the steps from the first tick that reach the maximum, then place each tick by index so that
        // rounding errors do not build up from adding the step repeatedly.
        var first = Math.Floor(min / step) * step;
        var count = (int)Math.Ceiling((max - first) / step - 1e-9) + 1;
        return Enumerable.Range(0, count).Select(i => Math.Round(first + i * step, 6)).ToList();
    }
}
