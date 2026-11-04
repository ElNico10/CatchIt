using Microsoft.Data.Sqlite;

namespace CatchIt.Core.Pricing;

/// <summary>
/// The "On Sale" rule (user story #8): a listing is on sale when its current price is clearly
/// below its recent average, not merely below it. Without a minimum gap about half of all listings
/// would count as "on sale", since a price is nearly always above or below its own average.
/// </summary>
public static class SaleCheck
{
    /// <summary>How far below the 90-day average a price must be: 10% (measured, this flags about 1 listing in 77).</summary>
    public const double MinGap = 0.10;

    /// <summary>How many days back the average looks.</summary>
    public const int AverageDays = 90;

    /// <summary>Whether a price counts as "on sale".</summary>
    /// <param name="currentPrice">The listing's current price.</param>
    /// <param name="average">
    /// The listing's 90-day average, or <c>null</c> when there is not enough history. Without an average
    /// nothing is flagged, so a listing is never wrongly shown as on sale.
    /// </param>
    /// <returns>True when the price is at least <see cref="MinGap"/> below the average. A price equal to the average is not on sale.</returns>
    public static bool IsOnSale(double currentPrice, double? average) =>
        average is { } avg && currentPrice <= avg * (1 - MinGap) + 1e-9; // the tiny allowance absorbs floating-point rounding at exactly 10%
}

/// <summary>
/// Looks up listings' recent average prices over one open connection, with the query prepared once so
/// it is cheap to run for every row of a results page.
/// </summary>
/// <remarks>
/// The day being asked about is left out of its own average, so a current price is never compared
/// with an average that already contains it. See <see cref="PriceHistoryService.GetAverage"/>.
/// </remarks>
internal sealed class AverageLookup : IDisposable
{
    private readonly SqliteCommand _command;
    private readonly SqliteParameter _listing;
    private readonly SqliteParameter _from;
    private readonly SqliteParameter _asOf;
    private readonly int _days;

    /// <summary>Creates the lookup.</summary>
    /// <param name="conn">The open connection to use; it must stay open while the lookup is in use.</param>
    /// <param name="days">How many days before the asked-about day the average covers.</param>
    public AverageLookup(SqliteConnection conn, int days)
    {
        _days = days;
        _command = conn.CreateCommand();
        _command.CommandText =
            "SELECT AVG(Price), COUNT(*) FROM PriceHistory WHERE ListingId = $l AND Date >= $from AND Date < $asOf";
        _listing = _command.Parameters.Add("$l", SqliteType.Integer);
        _from = _command.Parameters.Add("$from", SqliteType.Text);
        _asOf = _command.Parameters.Add("$asOf", SqliteType.Text);
    }

    /// <summary>Averages a listing's stored points from the days before <paramref name="asOf"/>.</summary>
    /// <param name="listingId">The listing to look up.</param>
    /// <param name="asOf">The day the average is for, normally today. That day itself is not included.</param>
    /// <returns>The average, or <c>null</c> when there are fewer than <see cref="PriceHistoryService.MinPointsForAverage"/> points.</returns>
    public double? Get(long listingId, DateOnly asOf)
    {
        _listing.Value = listingId;
        _from.Value = DateText.ToDb(asOf.AddDays(-_days));
        _asOf.Value = DateText.ToDb(asOf);

        using var reader = _command.ExecuteReader();
        reader.Read();
        return reader.GetInt64(1) >= PriceHistoryService.MinPointsForAverage ? reader.GetDouble(0) : null;
    }

    /// <summary>
    /// Averages many listings at once with a single query, which is much cheaper than one query per listing
    /// when marking a whole page of search results.
    /// </summary>
    /// <param name="conn">The open connection to use.</param>
    /// <param name="listingIds">The listings to look up.</param>
    /// <param name="days">How many days before <paramref name="asOf"/> the average covers.</param>
    /// <param name="asOf">The day the averages are for. That day itself is not included.</param>
    /// <returns>The average of each listing that has enough points; listings without enough history are left out.</returns>
    public static Dictionary<long, double> GetMany(SqliteConnection conn, IEnumerable<long> listingIds, int days, DateOnly asOf)
    {
        var averages = new Dictionary<long, double>();

        using var cmd = conn.CreateCommand();
        // The ids go in as one JSON array so a single statement can look them all up, whatever their number.
        cmd.CommandText = """
            SELECT ListingId, AVG(Price), COUNT(*) FROM PriceHistory
            WHERE ListingId IN (SELECT value FROM json_each($ids)) AND Date >= $from AND Date < $asOf
            GROUP BY ListingId
            """;
        cmd.Parameters.AddWithValue("$ids", "[" + string.Join(',', listingIds) + "]");
        cmd.Parameters.AddWithValue("$from", DateText.ToDb(asOf.AddDays(-days)));
        cmd.Parameters.AddWithValue("$asOf", DateText.ToDb(asOf));

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            if (reader.GetInt64(2) >= PriceHistoryService.MinPointsForAverage)
                averages[reader.GetInt64(0)] = reader.GetDouble(1);
        return averages;
    }

    public void Dispose() => _command.Dispose();
}
