using CatchIt.Core.Data;
using CatchIt.Core.Pricing;

namespace CatchIt.Core.Search;

/// <summary>
/// One watched product on the dashboard: its price against its target, and against its usual (90-day average) price.
/// </summary>
/// <param name="Item">The watched product, with its lowest current price and target price.</param>
/// <param name="Average90">The 90-day average price of the listing offering the current price, or <c>null</c> when there is not enough history.</param>
public record DashboardEntry(WatchedProduct Item, double? Average90)
{
    /// <summary>How far the price is from the target in dollars: negative when under it, positive when still above. Null without a price or target.</summary>
    public double? GapToTarget => Item.CurrentPrice - Item.TargetPrice;

    /// <summary>The gap to the target as a fraction of the target (-0.10 = 10% under it). Null without a price or target.</summary>
    public double? GapToTargetFraction => GapToTarget / Item.TargetPrice;

    /// <summary>
    /// The price against the 90-day average as a fraction (-0.12 = 12% below its usual price, 0.03 = 3% above).
    /// Null without a price or enough history. A target far below this number is probably unrealistic.
    /// </summary>
    public double? VersusAverageFraction => (Item.CurrentPrice - Average90) / Average90;
}

/// <summary>What the dashboard shows (user story #15).</summary>
/// <param name="BelowTarget">Watched products whose price has reached their target, biggest drop under the target first.</param>
/// <param name="NotYetAtTarget">Products with a target they have not reached, nearest to it first; products with no price come last.</param>
/// <param name="WithoutTarget">How many watched products have no target price set, so cannot be placed in either list.</param>
public record DashboardView(
    IReadOnlyList<DashboardEntry> BelowTarget, IReadOnlyList<DashboardEntry> NotYetAtTarget, int WithoutTarget);

/// <summary>
/// Builds the dashboard for the logged-in user (user story #15): which watched products have reached their
/// target price, so the user does not need email or push notifications to find out.
/// </summary>
/// <remarks>
/// The numbers are computed each time the dashboard opens, from the current prices; nothing is sent or stored.
/// Guests have no watchlist, so they get an empty view (the main window also keeps them out).
/// </remarks>
public class DashboardService
{
    private readonly WatchlistService _watchlist;
    private readonly Database _db;
    private readonly DateOnly? _today;

    /// <summary>Creates the service.</summary>
    /// <param name="watchlist">Supplies the user's watched products.</param>
    /// <param name="db">Where the price history is read from, for the 90-day averages.</param>
    /// <param name="today">The day "now" is; defaults to the real date. Tests pass a fixed date.</param>
    public DashboardService(WatchlistService watchlist, Database db, DateOnly? today = null)
    {
        _watchlist = watchlist;
        _db = db;
        _today = today;
    }

    /// <summary>The day the service treats as today.</summary>
    public DateOnly Today => _today ?? DateOnly.FromDateTime(DateTime.Today);

    /// <summary>Loads the dashboard for the logged-in user.</summary>
    public DashboardView Load()
    {
        var items = _watchlist.List();

        Dictionary<long, double> averages;
        using (var conn = _db.Open())
            averages = AverageLookup.GetMany(conn, items.Where(i => i.ListingId != null).Select(i => i.ListingId!.Value),
                SaleCheck.AverageDays, Today);

        var entries = items.Select(i => new DashboardEntry(
            i, i.ListingId is { } id && averages.TryGetValue(id, out var avg) ? avg : null)).ToList();

        var withTarget = entries.Where(e => e.Item.TargetPrice != null).ToList();

        // Most under the target first: that is the best news.
        var below = withTarget.Where(e => e.Item.IsBelowTarget)
            .OrderBy(e => e.GapToTargetFraction).ThenBy(e => e.Item.ProductName, StringComparer.OrdinalIgnoreCase).ToList();

        // Nearest to the target first; products with no price cannot be near anything, so they go last.
        var notYet = withTarget.Where(e => !e.Item.IsBelowTarget)
            .OrderBy(e => e.GapToTargetFraction ?? double.MaxValue).ThenBy(e => e.Item.ProductName, StringComparer.OrdinalIgnoreCase).ToList();

        return new DashboardView(below, notYet, entries.Count - withTarget.Count);
    }
}
