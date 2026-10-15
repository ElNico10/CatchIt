using System.Diagnostics;
using Microsoft.Data.Sqlite;

namespace CatchIt.Core.Data;

/// <summary>One product to import.</summary>
/// <param name="Name">The product name. Must be unique; a product whose name already exists is skipped.</param>
/// <param name="Brand">The brand name. Created if it does not exist yet.</param>
/// <param name="QualityTier">The brand's quality tier, 1 (Budget) to 3 (Premium). Only used when the brand is new.</param>
/// <param name="Keywords">Extra search words, such as the category. May be empty. The brand is added automatically.</param>
/// <param name="BasePrice">A typical current price in US dollars, used to build the simulated listings and history.</param>
public record ImportedProduct(string Name, string Brand, int QualityTier, string Keywords, double BasePrice);

/// <summary>What an import did.</summary>
/// <param name="ProductsAdded">New products created.</param>
/// <param name="ProductsSkipped">Products ignored because one with the same name already existed.</param>
/// <param name="ProductsRejected">Rows ignored because they were invalid (blank name or brand, bad tier, or non-positive price).</param>
/// <param name="ListingsAdded">Marketplace listings created.</param>
/// <param name="HistoryPointsAdded">Price history points stored.</param>
/// <param name="Elapsed">How long the import took.</param>
public record ImportSummary(
    int ProductsAdded, int ProductsSkipped, int ProductsRejected, int ListingsAdded, int HistoryPointsAdded, TimeSpan Elapsed);

/// <summary>
/// Loads a large catalog of products into the database quickly. The real products, brands and
/// prices come from the caller (for example a downloaded dataset); the marketplace listings,
/// seller ratings and price history are SIMULATED, as everywhere else in this app.
/// </summary>
/// <remarks>
/// For speed, the whole import runs in one transaction and the search index is rebuilt once at
/// the end instead of being updated row by row. If anything fails, the transaction is rolled
/// back, so the database is left exactly as it was, search triggers included.
/// Safe to run again: products whose names already exist are skipped.
/// </remarks>
public class BulkImporter
{
    /// <summary>How many days of simulated history each listing gets unless told otherwise.</summary>
    public const int DefaultHistoryDays = 400;

    private readonly Database _db;
    private readonly int _randomSeed;
    private readonly int _historyDays;
    private readonly DateOnly _today;

    /// <summary>Creates the importer.</summary>
    /// <param name="db">The database to import into.</param>
    /// <param name="randomSeed">Seed for the simulated data, so the same input gives the same result.</param>
    /// <param name="historyDays">
    /// How far back each listing's simulated history reaches. A smaller number means a
    /// smaller database, which matters a lot for very large catalogs.
    /// </param>
    /// <param name="today">The last day of every history. Defaults to today; tests pass a fixed date.</param>
    public BulkImporter(Database db, int randomSeed = 322, int historyDays = DefaultHistoryDays, DateOnly? today = null)
    {
        _db = db;
        _randomSeed = randomSeed;
        _historyDays = historyDays;
        _today = today ?? DateOnly.FromDateTime(DateTime.Today);
    }

    /// <summary>Imports the products.</summary>
    /// <param name="products">The products to add. Enumerated once, so it can be streamed from a file.</param>
    /// <returns>Counts of what was added, skipped and rejected.</returns>
    public ImportSummary Import(IEnumerable<ImportedProduct> products)
    {
        var watch = Stopwatch.StartNew();
        _db.Initialize();

        using var conn = _db.Open();
        using var tx = conn.BeginTransaction();

        var marketplaceIds = LoadOrCreateMarketplaces(conn, tx);

        // Row-by-row index updates would slow a big import down a lot; the index is rebuilt once at the end.
        SearchAndPricingSchema.DropSearchTriggers(conn, tx);

        int added = 0, skipped = 0, rejected = 0;
        var brandIds = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        using (var insertProduct = conn.CreateCommand())
        using (var lastId = conn.CreateCommand())
        using (var writer = new ListingWriter(conn, tx, new Random(_randomSeed), _today, _historyDays, marketplaceIds))
        {
            lastId.Transaction = tx;
            lastId.CommandText = "SELECT last_insert_rowid()";
            insertProduct.Transaction = tx;
            insertProduct.CommandText = "INSERT OR IGNORE INTO Products (Name, Keywords, BrandId) VALUES ($n, $k, $b)";
            var pName = insertProduct.Parameters.Add("$n", SqliteType.Text);
            var pKeywords = insertProduct.Parameters.Add("$k", SqliteType.Text);
            var pBrand = insertProduct.Parameters.Add("$b", SqliteType.Integer);

            foreach (var product in products)
            {
                if (!IsValid(product)) { rejected++; continue; }

                var brandId = GetOrCreateBrand(conn, tx, brandIds, product.Brand.Trim(), product.QualityTier);
                pName.Value = product.Name.Trim();
                // The brand is added to the keywords so searching for a brand name finds its products.
                pKeywords.Value = $"{product.Keywords} {product.Brand}".Trim();
                pBrand.Value = brandId;

                // OR IGNORE reports 0 rows changed when the name already exists.
                if (insertProduct.ExecuteNonQuery() == 0) { skipped++; continue; }

                added++;
                writer.AddListingsForProduct(Convert.ToInt64(lastId.ExecuteScalar()), product.BasePrice);
            }

            SearchAndPricingSchema.CreateSearchTriggers(conn, tx);
            SearchAndPricingSchema.RebuildSearchIndex(conn, tx);
            tx.Commit();

            return new ImportSummary(added, skipped, rejected, writer.ListingsAdded, writer.HistoryPointsAdded, watch.Elapsed);
        }
    }

    /// <summary>A row is usable only if it has a name, a brand, a tier from 1 to 3, and a positive price.</summary>
    private static bool IsValid(ImportedProduct p) =>
        !string.IsNullOrWhiteSpace(p.Name)
        && !string.IsNullOrWhiteSpace(p.Brand)
        && p.QualityTier is >= 1 and <= 3
        && p.BasePrice > 0
        && !double.IsInfinity(p.BasePrice);

    /// <summary>Returns the ids of all marketplaces, adding the standard demo ones if there are none yet.</summary>
    private static List<long> LoadOrCreateMarketplaces(SqliteConnection conn, SqliteTransaction tx)
    {
        var ids = ReadIds(conn, tx);
        if (ids.Count > 0) return ids;

        foreach (var name in Seeder.MarketplaceNames)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "INSERT OR IGNORE INTO Marketplaces (Name) VALUES ($n)";
            cmd.Parameters.AddWithValue("$n", name);
            cmd.ExecuteNonQuery();
        }
        return ReadIds(conn, tx);
    }

    private static List<long> ReadIds(SqliteConnection conn, SqliteTransaction tx)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT Id FROM Marketplaces ORDER BY Id";
        using var reader = cmd.ExecuteReader();
        var ids = new List<long>();
        while (reader.Read()) ids.Add(reader.GetInt64(0));
        return ids;
    }

    /// <summary>Finds a brand by name (remembering it for the next row), creating it if it is new.</summary>
    private static long GetOrCreateBrand(SqliteConnection conn, SqliteTransaction tx,
        Dictionary<string, long> cache, string name, int tier)
    {
        if (cache.TryGetValue(name, out var cached)) return cached;

        using (var insert = conn.CreateCommand())
        {
            insert.Transaction = tx;
            insert.CommandText = "INSERT OR IGNORE INTO Brands (Name, QualityTier) VALUES ($n, $t)";
            insert.Parameters.AddWithValue("$n", name);
            insert.Parameters.AddWithValue("$t", tier);
            insert.ExecuteNonQuery();
        }

        using var select = conn.CreateCommand();
        select.Transaction = tx;
        select.CommandText = "SELECT Id FROM Brands WHERE Name = $n";
        select.Parameters.AddWithValue("$n", name);
        var id = Convert.ToInt64(select.ExecuteScalar());
        cache[name] = id;
        return id;
    }
}
