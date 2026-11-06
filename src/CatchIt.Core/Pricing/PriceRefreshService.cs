using System.Globalization;
using CatchIt.Core.Auth;
using CatchIt.Core.Data;
using Microsoft.Data.Sqlite;

namespace CatchIt.Core.Pricing;

/// <summary>What a price refresh did.</summary>
/// <param name="Success">True when the refresh ran.</param>
/// <param name="Error">Why it did not run, when <paramref name="Success"/> is false.</param>
/// <param name="ListingsUpdated">How many listings got a new price.</param>
/// <param name="RefreshedAt">When the refresh ran (UTC), or null if it did not.</param>
public record PriceRefreshResult(bool Success, string? Error, int ListingsUpdated, DateTime? RefreshedAt)
{
    public static PriceRefreshResult Fail(string error) => new(false, error, 0, null);

    /// <summary>True when the refresh ran but there were no prices to update (no seeded products).</summary>
    public bool NothingToUpdate => Success && ListingsUpdated == 0;

    /// <summary>
    /// One line for the admin page and the command line: the error, "nothing to update",
    /// or how many listings changed and when (shown in local time).
    /// </summary>
    public string Describe() =>
        !Success ? Error!
        : NothingToUpdate ? PriceRefreshService.NothingToUpdateMessage
        : $"Updated {ListingsUpdated:N0} listings at {RefreshedAt!.Value.ToLocalTime():g}.";
}

/// <summary>One row of the refresh log: when a refresh ran and how many listings it changed.</summary>
public record RefreshLogEntry(DateTime RefreshedAt, int ListingsUpdated);

/// <summary>
/// Admin-only price refresh (issue #19). Gives every available listing a new SIMULATED current
/// price, records it in the price history, and logs the run in the <c>RefreshLog</c> table.
/// </summary>
/// <remarks>
/// There is no real marketplace to ask, so each price moves randomly by up to
/// <see cref="MaxChangePercent"/> percent, the way prices drift from day to day.
/// <para>
/// History keeps the same compact shape as <see cref="PriceHistoryService.RecordPrice"/>: the new
/// low and high are updated, and a history point is only added for a new low or high, when the
/// listing has no point in the last week, or to update a point already stored for today.
/// </para>
/// <para>
/// The whole refresh is a handful of SQL statements in one transaction, rather than one update
/// per listing, so it stays fast on the full imported catalog and either happens completely or
/// not at all. Listings with no current price (unavailable) are left alone.
/// </para>
/// </remarks>
public class PriceRefreshService
{
    /// <summary>The most a single refresh can move a price, up or down, in percent.</summary>
    public const int MaxChangePercent = 5;

    /// <summary>Shown when the database has no listings with a price (issue #19, scenario 3).</summary>
    public const string NothingToUpdateMessage = "There are no prices to update. Seed the database first (--seed).";

    private readonly Database _db;
    private readonly Session _session;
    private readonly Func<DateTime> _now;

    /// <summary>Creates the service.</summary>
    /// <param name="db">The database holding the listings.</param>
    /// <param name="session">Used to check that an administrator is logged in.</param>
    /// <param name="now">Clock, overridable for tests. Defaults to the current UTC time.</param>
    public PriceRefreshService(Database db, Session session, Func<DateTime>? now = null)
    {
        _db = db;
        _session = session;
        _now = now ?? (() => DateTime.UtcNow);
    }

    /// <summary>Refreshes every available listing's price, if the logged-in user is an administrator.</summary>
    /// <returns>
    /// How many listings were updated, or a failure explaining why nothing changed. With no priced
    /// listings at all it succeeds with <see cref="PriceRefreshResult.NothingToUpdate"/> set, changes
    /// nothing and logs nothing, since no refresh actually happened.
    /// </returns>
    public PriceRefreshResult Refresh()
    {
        var access = Access.Check(_session, Feature.PriceRefresh);
        if (!access.Allowed)
            return PriceRefreshResult.Fail(access.Reason!);

        var now = _now();
        var today = DateText.ToDb(DateOnly.FromDateTime(now));
        var weekAgo = DateText.ToDb(DateOnly.FromDateTime(now).AddDays(-HistoryCompactor.SampleIntervalDays));

        using var conn = _db.Open();

        // Scenario 3: no seeded products. Report it instead of running (and logging) an empty refresh.
        using (var any = conn.CreateCommand())
        {
            any.CommandText = "SELECT EXISTS (SELECT 1 FROM Listings WHERE CurrentPrice IS NOT NULL)";
            if (Convert.ToInt64(any.ExecuteScalar()) == 0)
                return new PriceRefreshResult(true, null, 0, now);
        }

        using var tx = conn.BeginTransaction();

        // 1. Work out every new price first. random() gives a 64-bit integer; this turns it into a
        //    change between -MaxChangePercent% and +MaxChangePercent%, rounded to whole cents.
        //    IsExtreme marks prices that beat the listing's stored low or high (or that have none).
        Run(conn, tx, $"""
            CREATE TEMP TABLE RefreshPrices (
                ListingId INTEGER PRIMARY KEY,
                Price REAL NOT NULL,
                IsExtreme INTEGER NOT NULL DEFAULT 0
            );
            INSERT INTO RefreshPrices (ListingId, Price)
            SELECT Id, max(0.01, round(CurrentPrice * (1 + ((abs(random()) % {2 * MaxChangePercent * 100 + 1}) - {MaxChangePercent * 100}) / 10000.0), 2))
            FROM Listings
            WHERE CurrentPrice IS NOT NULL;

            UPDATE RefreshPrices SET IsExtreme = coalesce(
                (SELECT RefreshPrices.Price < e.LowPrice OR RefreshPrices.Price > e.HighPrice
                 FROM PriceExtremes e WHERE e.ListingId = RefreshPrices.ListingId),
                1);
            """);

        // 2. Current prices.
        Run(conn, tx, """
            UPDATE Listings
            SET CurrentPrice = (SELECT Price FROM RefreshPrices r WHERE r.ListingId = Listings.Id)
            WHERE Id IN (SELECT ListingId FROM RefreshPrices);
            """);

        // 3. Lowest and highest prices. Equal prices do not move a date: the earliest day stays.
        Run(conn, tx, """
            INSERT INTO PriceExtremes (ListingId, LowPrice, LowDate, HighPrice, HighDate)
            SELECT r.ListingId, r.Price, $today, r.Price, $today
            FROM RefreshPrices r
            WHERE NOT EXISTS (SELECT 1 FROM PriceExtremes e WHERE e.ListingId = r.ListingId);

            UPDATE PriceExtremes
            SET LowPrice = (SELECT Price FROM RefreshPrices r WHERE r.ListingId = PriceExtremes.ListingId), LowDate = $today
            WHERE (SELECT Price FROM RefreshPrices r WHERE r.ListingId = PriceExtremes.ListingId) < LowPrice;

            UPDATE PriceExtremes
            SET HighPrice = (SELECT Price FROM RefreshPrices r WHERE r.ListingId = PriceExtremes.ListingId), HighDate = $today
            WHERE (SELECT Price FROM RefreshPrices r WHERE r.ListingId = PriceExtremes.ListingId) > HighPrice;
            """, ("$today", today));

        // 4. History points, keeping roughly one a week plus the extremes.
        Run(conn, tx, """
            INSERT INTO PriceHistory (ListingId, Date, Price)
            SELECT r.ListingId, $today, r.Price
            FROM RefreshPrices r
            WHERE r.IsExtreme = 1
               OR NOT EXISTS (SELECT 1 FROM PriceHistory h
                              WHERE h.ListingId = r.ListingId AND h.Date > $weekAgo AND h.Date < $today)
            ON CONFLICT (ListingId, Date) DO UPDATE SET Price = excluded.Price;
            """, ("$today", today), ("$weekAgo", weekAgo));

        // 5. Log the run and clean up.
        var updated = Convert.ToInt32(Scalar(conn, tx, "SELECT COUNT(*) FROM RefreshPrices"));
        Run(conn, tx, "INSERT INTO RefreshLog (RefreshedAt, ListingsUpdated) VALUES ($at, $n);",
            ("$at", now.ToString("O", CultureInfo.InvariantCulture)), ("$n", updated));
        Run(conn, tx, "DROP TABLE RefreshPrices;");

        tx.Commit();
        return new PriceRefreshResult(true, null, updated, now);
    }

    /// <summary>The most recent refresh, or null if prices have never been refreshed.</summary>
    public RefreshLogEntry? LastRefresh()
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT RefreshedAt, ListingsUpdated FROM RefreshLog ORDER BY Id DESC LIMIT 1";
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            return null;
        var at = DateTime.Parse(reader.GetString(0), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        return new RefreshLogEntry(at, reader.GetInt32(1));
    }

    private static void Run(SqliteConnection conn, SqliteTransaction tx, string sql, params (string Name, object Value)[] args)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        foreach (var (name, value) in args) cmd.Parameters.AddWithValue(name, value);
        cmd.ExecuteNonQuery();
    }

    private static object? Scalar(SqliteConnection conn, SqliteTransaction tx, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        return cmd.ExecuteScalar();
    }
}
