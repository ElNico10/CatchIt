using Microsoft.Data.Sqlite;

namespace CatchIt.Core.Data;

/// <summary>
/// Works out what a freshly imported catalog does not say itself: how premium each brand is,
/// and which products are sensible alternatives to which. Both are derived from the products'
/// real prices and ratings, so nothing has to be curated by hand.
/// </summary>
/// <remarks>
/// Products are compared only with other products of the same category. Both steps read the
/// products sorted by category and price, one category at a time, so memory use is bounded by
/// the largest single category rather than the whole catalog.
/// </remarks>
public static class CatalogEnricher
{
    /// <summary>Categories with fewer products than this are too small to say what a "cheap" or "expensive" price is.</summary>
    public const int MinCategorySize = 10;

    /// <summary>A brand needs at least this many products in sized categories before its tier is derived; otherwise it stays as it is.</summary>
    public const int MinBrandProducts = 3;

    /// <summary>How many products on each side of a product (in price order) are considered as alternatives.</summary>
    public const int NeighbourWindow = 25;

    /// <summary>The most cheaper, lower-tier alternatives stored per product.</summary>
    public const int CheaperPicks = 2;

    /// <summary>The most better-rated alternatives stored per product.</summary>
    public const int BetterRatedPicks = 2;

    /// <summary>An average rating counts only if it rests on at least this many ratings.</summary>
    public const int MinRatingsToTrust = 10;

    private sealed record Row(long Id, long BrandId, int Tier, double Price, double? Rating, int RatingCount);

    /// <summary>
    /// Sets each brand's quality tier from where its products' prices fall within their categories:
    /// the cheapest third of a category counts as Budget, the middle third as Standard and the top
    /// third as Premium, and a brand gets the rounded average of its products' bands.
    /// </summary>
    /// <param name="db">The database to update.</param>
    /// <returns>How many brands were given a derived tier.</returns>
    public static int DeriveBrandTiers(Database db)
    {
        using var conn = db.Open();
        var bands = new Dictionary<long, (long Sum, int Count)>();

        foreach (var group in ReadCategories(conn))
        {
            if (group.Count < MinCategorySize) continue;

            // Thresholds are prices (not positions) so products with the same price always get the same band.
            var lowCut = group[group.Count / 3].Price;
            var highCut = group[group.Count * 2 / 3].Price;
            foreach (var row in group)
            {
                var band = row.Price < lowCut ? 1 : row.Price < highCut ? 2 : 3;
                bands.TryGetValue(row.BrandId, out var seen);
                bands[row.BrandId] = (seen.Sum + band, seen.Count + 1);
            }
        }

        using var tx = conn.BeginTransaction();
        using var update = conn.CreateCommand();
        update.Transaction = tx;
        update.CommandText = "UPDATE Brands SET QualityTier = $t WHERE Id = $id";
        var pTier = update.Parameters.Add("$t", SqliteType.Integer);
        var pId = update.Parameters.Add("$id", SqliteType.Integer);

        var updated = 0;
        foreach (var (brandId, (sum, count)) in bands)
        {
            if (count < MinBrandProducts) continue;
            pTier.Value = (int)Math.Round((double)sum / count, MidpointRounding.AwayFromZero);
            pId.Value = brandId;
            update.ExecuteNonQuery();
            updated++;
        }
        tx.Commit();
        return updated;
    }

    /// <summary>
    /// Stores, for each product, a few alternatives from the same category and a different brand,
    /// picked from the products priced closest to it:
    /// up to <see cref="CheaperPicks"/> that are cheaper and of a lower brand tier, and up to
    /// <see cref="BetterRatedPicks"/> with a better (and trusted) rating. The product detail page chooses
    /// what to show from this stored list; no similarity is worked out at display time.
    /// </summary>
    /// <param name="db">The database to add the links to. Existing links are kept.</param>
    /// <returns>How many new links were stored.</returns>
    public static int BuildAlternatives(Database db)
    {
        using var conn = db.Open();
        using var tx = conn.BeginTransaction();
        using var insert = conn.CreateCommand();
        insert.Transaction = tx;
        insert.CommandText = "INSERT OR IGNORE INTO Alternatives (ProductId, AlternativeProductId) VALUES ($p, $a)";
        var pProduct = insert.Parameters.Add("$p", SqliteType.Integer);
        var pAlternative = insert.Parameters.Add("$a", SqliteType.Integer);

        var added = 0;
        foreach (var group in ReadCategories(conn))
        {
            for (var i = 0; i < group.Count; i++)
            {
                var product = group[i];
                var picks = new List<Row>();

                // Cheaper and lower tier: walk down from the product so the nearest prices come first.
                for (var j = i - 1; j >= Math.Max(0, i - NeighbourWindow) && picks.Count < CheaperPicks; j--)
                {
                    var other = group[j];
                    if (other.Price < product.Price && other.Tier < product.Tier && other.BrandId != product.BrandId)
                        picks.Add(other);
                }

                var better = new List<Row>();
                for (var j = Math.Max(0, i - NeighbourWindow); j <= Math.Min(group.Count - 1, i + NeighbourWindow); j++)
                {
                    var other = group[j];
                    if (j == i || other.BrandId == product.BrandId || picks.Contains(other)) continue;
                    if (other.Rating is not { } rating || other.RatingCount < MinRatingsToTrust) continue;
                    if (rating > (product.Rating ?? 0)) better.Add(other);
                }
                picks.AddRange(better.OrderByDescending(r => r.Rating).ThenByDescending(r => r.RatingCount).Take(BetterRatedPicks));

                foreach (var pick in picks)
                {
                    pProduct.Value = product.Id;
                    pAlternative.Value = pick.Id;
                    added += insert.ExecuteNonQuery();
                }
            }
        }

        tx.Commit();
        return added;
    }

    /// <summary>
    /// Reads every product that has a category and a price, one category at a time, cheapest first.
    /// </summary>
    private static IEnumerable<List<Row>> ReadCategories(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT p.Category, p.Id, p.BrandId, b.QualityTier, p.TypicalPrice, p.AverageRating, p.RatingCount
            FROM Products p JOIN Brands b ON b.Id = p.BrandId
            WHERE p.Category <> '' AND p.TypicalPrice IS NOT NULL
            ORDER BY p.Category, p.TypicalPrice, p.Id
            """;
        using var reader = cmd.ExecuteReader();

        string? category = null;
        var group = new List<Row>();
        while (reader.Read())
        {
            var current = reader.GetString(0);
            if (category != null && current != category)
            {
                yield return group;
                group = new List<Row>();
            }
            category = current;
            group.Add(new Row(
                reader.GetInt64(1), reader.GetInt64(2), reader.GetInt32(3), reader.GetDouble(4),
                reader.IsDBNull(5) ? null : reader.GetDouble(5), reader.IsDBNull(6) ? 0 : reader.GetInt32(6)));
        }
        if (group.Count > 0) yield return group;
    }
}
