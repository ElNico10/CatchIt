using CatchIt.Core.Data;
using Microsoft.Data.Sqlite;

namespace CatchIt.Core.Pricing;

/// <summary>
/// Reads and updates listing price history. History is stored compactly: about one point a
/// week, plus the day of the lowest and the day of the highest price (see
/// <see cref="HistoryCompactor"/>). This service keeps that shape when new prices arrive.
/// </summary>
public class PriceHistoryService
{
    /// <summary>
    /// The fewest stored points needed to call an average meaningful. With weekly samples,
    /// fewer than two points means under a week of history, so there is "not enough history".
    /// </summary>
    public const int MinPointsForAverage = 2;

    private readonly Database _db;

    /// <summary>Creates the service.</summary>
    /// <param name="db">The database holding the history.</param>
    public PriceHistoryService(Database db)
    {
        _db = db;
    }

    /// <summary>Gets the absolute low and high of a listing, or <c>null</c> if none are recorded.</summary>
    /// <param name="listingId">The listing to look up.</param>
    public PriceExtremes? GetExtremes(long listingId)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT LowPrice, LowDate, HighPrice, HighDate FROM PriceExtremes WHERE ListingId = $l";
        cmd.Parameters.AddWithValue("$l", listingId);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return new PriceExtremes(listingId, reader.GetDouble(0), DateText.FromDb(reader.GetString(1)),
            reader.GetDouble(2), DateText.FromDb(reader.GetString(3)));
    }

    /// <summary>Gets the stored history points in a date range, oldest first.</summary>
    /// <param name="listingId">The listing to look up.</param>
    /// <param name="from">First day to include.</param>
    /// <param name="to">Last day to include.</param>
    public IReadOnlyList<PricePoint> GetPoints(long listingId, DateOnly from, DateOnly to)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Date, Price FROM PriceHistory WHERE ListingId = $l AND Date >= $from AND Date <= $to ORDER BY Date";
        cmd.Parameters.AddWithValue("$l", listingId);
        cmd.Parameters.AddWithValue("$from", DateText.ToDb(from));
        cmd.Parameters.AddWithValue("$to", DateText.ToDb(to));

        var points = new List<PricePoint>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            points.Add(new PricePoint(DateText.FromDb(reader.GetString(0)), reader.GetDouble(1)));
        return points;
    }

    /// <summary>
    /// Averages the stored points from the <paramref name="days"/> days before
    /// <paramref name="asOf"/>. The day <paramref name="asOf"/> itself is left out, so a
    /// product's current price is not compared against an average that already contains it.
    /// </summary>
    /// <param name="listingId">The listing to look up.</param>
    /// <param name="days">How many days back to look (90 for the "On Sale" check).</param>
    /// <param name="asOf">The day the average is for, normally today.</param>
    /// <returns>The average price, or <c>null</c> when there are fewer than <see cref="MinPointsForAverage"/> points.</returns>
    public double? GetAverage(long listingId, int days, DateOnly asOf)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT AVG(Price), COUNT(*) FROM PriceHistory WHERE ListingId = $l AND Date >= $from AND Date < $asOf";
        cmd.Parameters.AddWithValue("$l", listingId);
        cmd.Parameters.AddWithValue("$from", DateText.ToDb(asOf.AddDays(-days)));
        cmd.Parameters.AddWithValue("$asOf", DateText.ToDb(asOf));

        using var reader = cmd.ExecuteReader();
        reader.Read();
        return reader.GetInt64(1) >= MinPointsForAverage ? reader.GetDouble(0) : null;
    }

    /// <summary>
    /// Records a newly seen price, for example from a price refresh. The listing's current
    /// price is always updated and the extremes are checked against the new price. A history
    /// point is stored only if none exists in the past week, or if the price is a new low or
    /// high (so the dip or peak shows on the chart).
    /// </summary>
    /// <param name="listingId">The listing the price belongs to.</param>
    /// <param name="date">The day the price applies to. Expected to be today or later than earlier calls.</param>
    /// <param name="price">The price in US dollars. Must be positive.</param>
    /// <exception cref="ArgumentOutOfRangeException">The price is zero, negative or not a number.</exception>
    /// <exception cref="ArgumentException">There is no listing with that id.</exception>
    public void RecordPrice(long listingId, DateOnly date, double price)
    {
        if (!(price > 0) || double.IsInfinity(price))
            throw new ArgumentOutOfRangeException(nameof(price), "The price must be a positive number.");

        using var conn = _db.Open();
        using var tx = conn.BeginTransaction();

        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "UPDATE Listings SET CurrentPrice = $p WHERE Id = $l";
            cmd.Parameters.AddWithValue("$p", price);
            cmd.Parameters.AddWithValue("$l", listingId);
            if (cmd.ExecuteNonQuery() == 0)
                throw new ArgumentException($"There is no listing with id {listingId}.", nameof(listingId));
        }

        var isNewExtreme = UpdateExtremes(conn, tx, listingId, date, price);

        // "Recent" means within the last week up to and including this date.
        bool hasRecentPoint;
        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "SELECT 1 FROM PriceHistory WHERE ListingId = $l AND Date > $cutoff AND Date <= $date LIMIT 1";
            cmd.Parameters.AddWithValue("$l", listingId);
            cmd.Parameters.AddWithValue("$cutoff", DateText.ToDb(date.AddDays(-HistoryCompactor.SampleIntervalDays)));
            cmd.Parameters.AddWithValue("$date", DateText.ToDb(date));
            hasRecentPoint = cmd.ExecuteScalar() != null;
        }

        if (isNewExtreme || !hasRecentPoint)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            // If a point for this exact day exists, the newer price replaces it.
            cmd.CommandText =
                "INSERT INTO PriceHistory (ListingId, Date, Price) VALUES ($l, $d, $p) " +
                "ON CONFLICT (ListingId, Date) DO UPDATE SET Price = excluded.Price";
            cmd.Parameters.AddWithValue("$l", listingId);
            cmd.Parameters.AddWithValue("$d", DateText.ToDb(date));
            cmd.Parameters.AddWithValue("$p", price);
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
    }

    /// <summary>
    /// Creates the extremes row if the listing has none, otherwise lowers the low or raises the
    /// high when the new price beats it. Equal prices do not move a date: the earliest day stays.
    /// </summary>
    /// <returns>True when the price set a new low or high (or created the first extremes).</returns>
    private static bool UpdateExtremes(SqliteConnection conn, SqliteTransaction tx, long listingId, DateOnly date, double price)
    {
        double low, high;
        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "SELECT LowPrice, HighPrice FROM PriceExtremes WHERE ListingId = $l";
            cmd.Parameters.AddWithValue("$l", listingId);
            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                reader.Close();
                using var insert = conn.CreateCommand();
                insert.Transaction = tx;
                insert.CommandText =
                    "INSERT INTO PriceExtremes (ListingId, LowPrice, LowDate, HighPrice, HighDate) VALUES ($l, $p, $d, $p, $d)";
                insert.Parameters.AddWithValue("$l", listingId);
                insert.Parameters.AddWithValue("$p", price);
                insert.Parameters.AddWithValue("$d", DateText.ToDb(date));
                insert.ExecuteNonQuery();
                return true;
            }
            low = reader.GetDouble(0);
            high = reader.GetDouble(1);
        }

        var newLow = price < low;
        var newHigh = price > high;
        if (newLow)
            Update(conn, tx, "UPDATE PriceExtremes SET LowPrice = $p, LowDate = $d WHERE ListingId = $l", listingId, date, price);
        if (newHigh)
            Update(conn, tx, "UPDATE PriceExtremes SET HighPrice = $p, HighDate = $d WHERE ListingId = $l", listingId, date, price);
        return newLow || newHigh;
    }

    private static void Update(SqliteConnection conn, SqliteTransaction tx, string sql, long listingId, DateOnly date, double price)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("$l", listingId);
        cmd.Parameters.AddWithValue("$d", DateText.ToDb(date));
        cmd.Parameters.AddWithValue("$p", price);
        cmd.ExecuteNonQuery();
    }
}
