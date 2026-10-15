using CatchIt.Core.Pricing;
using Microsoft.Data.Sqlite;

namespace CatchIt.Core.Data;

/// <summary>
/// Creates the marketplace listings for a product, together with their simulated price
/// history, inside a transaction the caller owns. Shared by the demo <see cref="Seeder"/>
/// and the <see cref="BulkImporter"/> so both produce history the same way.
/// </summary>
/// <remarks>
/// The SQL commands are prepared once and reused for every row, which is what makes
/// importing large catalogs fast.
/// </remarks>
internal sealed class ListingWriter : IDisposable
{
    private readonly Random _rng;
    private readonly DateOnly _today;
    private readonly int _historyDays;
    private readonly IReadOnlyList<long> _marketplaceIds;

    private readonly SqliteCommand _insertListing;
    private readonly SqliteCommand _insertPoint;
    private readonly SqliteCommand _insertExtremes;
    private readonly SqliteCommand _lastRowId;

    /// <summary>How many listings have been written so far.</summary>
    public int ListingsAdded { get; private set; }

    /// <summary>How many history points have been written so far.</summary>
    public int HistoryPointsAdded { get; private set; }

    /// <summary>Creates the writer.</summary>
    /// <param name="conn">The open connection.</param>
    /// <param name="tx">The transaction every write belongs to.</param>
    /// <param name="rng">Randomness for ratings, price levels and history; seed it for repeatable data.</param>
    /// <param name="today">The last day of every price history.</param>
    /// <param name="historyDays">How far back each history reaches.</param>
    /// <param name="marketplaceIds">The marketplaces a product can be listed on.</param>
    public ListingWriter(SqliteConnection conn, SqliteTransaction tx, Random rng, DateOnly today,
        int historyDays, IReadOnlyList<long> marketplaceIds)
    {
        _rng = rng;
        _today = today;
        _historyDays = historyDays;
        _marketplaceIds = marketplaceIds;

        _insertListing = Prepare(conn, tx,
            "INSERT INTO Listings (ProductId, MarketplaceId, SellerRating, CurrentPrice) VALUES ($p, $m, $r, $c)",
            "$p", "$m", "$r", "$c");
        _insertPoint = Prepare(conn, tx,
            "INSERT OR IGNORE INTO PriceHistory (ListingId, Date, Price) VALUES ($l, $d, $p)",
            "$l", "$d", "$p");
        _insertExtremes = Prepare(conn, tx,
            "INSERT INTO PriceExtremes (ListingId, LowPrice, LowDate, HighPrice, HighDate) VALUES ($l, $lp, $ld, $hp, $hd)",
            "$l", "$lp", "$ld", "$hp", "$hd");
        _lastRowId = conn.CreateCommand();
        _lastRowId.Transaction = tx;
        _lastRowId.CommandText = "SELECT last_insert_rowid()";
    }

    /// <summary>
    /// Lists a product on a random selection of marketplaces (each carries it with 75% chance,
    /// and at least one always does), each with its own price level, seller rating and history.
    /// </summary>
    /// <param name="productId">The product to list.</param>
    /// <param name="basePrice">The typical price; each marketplace varies it by up to 10%.</param>
    public void AddListingsForProduct(long productId, double basePrice)
    {
        var carriedBy = Enumerable.Range(0, _marketplaceIds.Count).Where(_ => _rng.NextDouble() < 0.75).ToList();
        if (carriedBy.Count == 0) carriedBy.Add(0);

        foreach (var marketIndex in carriedBy)
        {
            var rating = Math.Round(2.5 + _rng.NextDouble() * 2.5, 1);
            var marketPrice = basePrice * (0.9 + _rng.NextDouble() * 0.2);

            // The history runs up to today, so today's price is the listing's current price.
            var series = PriceHistoryGenerator.Generate(marketPrice, marketIndex, _today, _historyDays, _rng);
            var compact = HistoryCompactor.Compact(series);
            var current = series[^1].Price;

            Set(_insertListing, productId, _marketplaceIds[marketIndex], rating, current);
            _insertListing.ExecuteNonQuery();
            var listingId = Convert.ToInt64(_lastRowId.ExecuteScalar());
            ListingsAdded++;

            foreach (var point in compact.Points)
            {
                Set(_insertPoint, listingId, DateText.ToDb(point.Date), point.Price);
                HistoryPointsAdded += _insertPoint.ExecuteNonQuery();
            }

            Set(_insertExtremes, listingId, compact.Low.Price, DateText.ToDb(compact.Low.Date),
                compact.High.Price, DateText.ToDb(compact.High.Date));
            _insertExtremes.ExecuteNonQuery();
        }
    }

    public void Dispose()
    {
        _insertListing.Dispose();
        _insertPoint.Dispose();
        _insertExtremes.Dispose();
        _lastRowId.Dispose();
    }

    /// <summary>Creates a command with named parameters so it can be reused for many rows.</summary>
    private static SqliteCommand Prepare(SqliteConnection conn, SqliteTransaction tx, string sql, params string[] parameters)
    {
        var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;

        // No explicit type: each value is bound as its own .NET type (integer, real or text).
        foreach (var name in parameters)
        {
            var parameter = cmd.CreateParameter();
            parameter.ParameterName = name;
            cmd.Parameters.Add(parameter);
        }
        return cmd;
    }

    /// <summary>Fills a prepared command's parameters, in the order they were declared.</summary>
    private static void Set(SqliteCommand cmd, params object[] values)
    {
        for (var i = 0; i < values.Length; i++)
            cmd.Parameters[i].Value = values[i];
    }
}
