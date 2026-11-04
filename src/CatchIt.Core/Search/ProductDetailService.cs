using CatchIt.Core.Data;
using CatchIt.Core.Pricing;

namespace CatchIt.Core.Search;

/// <summary>One suggested alternative to the product being viewed (user story #12).</summary>
/// <param name="ListingId">The alternative's cheapest listing.</param>
/// <param name="ProductId">The alternative product.</param>
/// <param name="ProductName">The product name.</param>
/// <param name="BrandName">The brand.</param>
/// <param name="QualityTier">The brand's quality tier: 1 = Budget, 2 = Standard, 3 = Premium.</param>
/// <param name="MarketplaceName">The marketplace with the lowest price. SIMULATED.</param>
/// <param name="Price">The lowest current price. SIMULATED.</param>
/// <param name="Rating">The product's average review rating (0 to 5), if known.</param>
/// <param name="RatingCount">How many ratings the average rests on, if known.</param>
/// <param name="Tags">
/// Why it is suggested, for example "Cheapest option" or "May drop soon: ...". One product that earns several
/// reasons appears once, with all of them.
/// </param>
public record AlternativeSuggestion(
    long ListingId, long ProductId, string ProductName, string BrandName, int QualityTier, string MarketplaceName,
    double Price, double? Rating, int? RatingCount, IReadOnlyList<string> Tags);

/// <summary>Everything the product detail window shows about one listing.</summary>
/// <param name="Listing">The listing itself (product, brand, tier, marketplace, seller rating, price, On Sale flag).</param>
/// <param name="Category">The product's category path, or empty if unknown.</param>
/// <param name="ProductRating">The product's average review rating (0 to 5), if known.</param>
/// <param name="RatingCount">How many ratings that average rests on, if known.</param>
/// <param name="Extremes">The all-time lowest and highest price with their dates, if recorded.</param>
/// <param name="Average90">The 90-day average price, or <c>null</c> when there is not enough history.</param>
/// <param name="History">The stored price points for the chart, oldest first.</param>
/// <param name="Alternatives">The suggested alternatives, at most three.</param>
public record ProductDetail(
    ListingHit Listing, string Category, double? ProductRating, int? RatingCount, PriceExtremes? Extremes,
    double? Average90, IReadOnlyList<PricePoint> History, IReadOnlyList<AlternativeSuggestion> Alternatives);

/// <summary>
/// Gathers what the product detail window shows: the listing, its price history, and up to three
/// suggested alternatives chosen from the stored alternatives of the product.
/// </summary>
/// <remarks>
/// The alternatives are never worked out from scratch here. They come from the stored list built when the
/// catalog was imported (or curated), and this class only picks which of them to show:
/// <list type="bullet">
/// <item><description><b>Cheapest option</b>: cheaper and of a lower brand tier (story #12), the lowest price wins.</description></item>
/// <item><description><b>Better reviewed</b>: the highest rating that beats this product's, resting on enough ratings to trust.</description></item>
/// <item><description><b>May drop soon</b>: the biggest drop (at least 10%) in the same stretch of last year; see <see cref="SeasonalDrops"/>.</description></item>
/// </list>
/// A product that wins more than one slot is shown once with every tag. Everything is read when the window opens,
/// from at most a handful of indexed lookups, so nothing is precomputed for the whole catalog.
/// </remarks>
public class ProductDetailService
{
    private readonly Database _db;
    private readonly PriceHistoryService _history;
    private readonly DateOnly? _today;

    /// <summary>Creates the service.</summary>
    /// <param name="db">The database to read.</param>
    /// <param name="today">The day "now" is; defaults to the real date. Tests pass a fixed date.</param>
    public ProductDetailService(Database db, DateOnly? today = null)
    {
        _db = db;
        _history = new PriceHistoryService(db);
        _today = today;
    }

    /// <summary>The day the service treats as today.</summary>
    public DateOnly Today => _today ?? DateOnly.FromDateTime(DateTime.Today);

    private sealed record Candidate(
        long ProductId, string Name, string Brand, int Tier, double? Rating, int? RatingCount,
        long ListingId, string Marketplace, double Price);

    /// <summary>Loads the detail for one listing.</summary>
    /// <param name="listingId">The listing, as shown in the search results.</param>
    /// <returns>The detail, or <c>null</c> if there is no such listing.</returns>
    public ProductDetail? GetDetail(long listingId)
    {
        var asOf = Today;
        ListingHit listing;
        string category;
        double? rating;
        int? ratingCount;

        using (var conn = _db.Open())
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                SELECT l.Id, p.Id, p.Name, b.Name, b.QualityTier, m.Name, l.SellerRating, l.CurrentPrice,
                       p.Category, p.AverageRating, p.RatingCount
                FROM Listings l
                JOIN Products p ON p.Id = l.ProductId
                JOIN Brands b ON b.Id = p.BrandId
                JOIN Marketplaces m ON m.Id = l.MarketplaceId
                WHERE l.Id = $l
                """;
            cmd.Parameters.AddWithValue("$l", listingId);
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;

            listing = new ListingHit(r.GetInt64(0), r.GetInt64(1), r.GetString(2), r.GetString(3), r.GetInt32(4),
                r.GetString(5), r.GetDouble(6), r.IsDBNull(7) ? null : r.GetDouble(7));
            category = r.GetString(8);
            rating = r.IsDBNull(9) ? null : r.GetDouble(9);
            ratingCount = r.IsDBNull(10) ? null : r.GetInt32(10);
        }

        var average = _history.GetAverage(listingId, SaleCheck.AverageDays, asOf);
        listing = listing with { OnSale = listing.CurrentPrice is { } price && SaleCheck.IsOnSale(price, average) };

        return new ProductDetail(
            listing, category, rating, ratingCount,
            _history.GetExtremes(listingId), average,
            _history.GetPoints(listingId, asOf.AddYears(-3), asOf),
            ChooseAlternatives(listing, rating, asOf));
    }

    private IReadOnlyList<AlternativeSuggestion> ChooseAlternatives(ListingHit current, double? ownRating, DateOnly asOf)
    {
        var candidates = LoadCandidates(current.ProductId);
        if (candidates.Count == 0) return Array.Empty<AlternativeSuggestion>();

        // Each pick is (candidate, tag). Slots are filled in this order, and the cards appear in it.
        var picks = new List<(Candidate Candidate, string Tag)>();

        if (current.CurrentPrice is { } currentPrice)
        {
            var cheapest = candidates.Where(c => c.Price < currentPrice && c.Tier < current.QualityTier)
                                     .OrderBy(c => c.Price).ThenBy(c => c.ProductId).FirstOrDefault();
            if (cheapest != null) picks.Add((cheapest, "Cheapest option"));
        }

        var better = candidates
            .Where(c => c.Rating is { } r && c.RatingCount >= CatalogEnricher.MinRatingsToTrust && r > (ownRating ?? 0))
            .OrderByDescending(c => c.Rating).ThenByDescending(c => c.RatingCount).ThenBy(c => c.ProductId)
            .FirstOrDefault();
        if (better != null) picks.Add((better, "Better reviewed"));

        var drops = candidates
            .Select(c => (Candidate: c, Drop: SeasonalDrops.Find(
                _history.GetPoints(c.ListingId, SeasonalDrops.WindowStart(asOf), SeasonalDrops.WindowEnd(asOf)), c.Price)))
            .Where(x => x.Drop != null)
            .OrderByDescending(x => x.Drop!.Percent).ThenBy(x => x.Candidate.ProductId)
            .FirstOrDefault();
        if (drops.Drop != null)
            picks.Add((drops.Candidate, DropTag(drops.Drop)));

        // One card per product, with every reason it earned, in the order the slots were filled.
        return picks.GroupBy(p => p.Candidate.ProductId)
            .Select(g =>
            {
                var c = g.First().Candidate;
                return new AlternativeSuggestion(c.ListingId, c.ProductId, c.Name, c.Brand, c.Tier, c.Marketplace,
                    c.Price, c.Rating, c.RatingCount, g.Select(p => p.Tag).ToList());
            })
            .ToList();
    }

    /// <summary>"May drop soon: was about 34% cheaper around Black Friday / Cyber Monday last year".</summary>
    private static string DropTag(SeasonalDrop drop) =>
        $"May drop soon: was about {drop.Percent:0}% cheaper around {drop.EventName ?? "this time"} last year";

    /// <summary>The stored alternatives of a product, each with its cheapest priced listing (alternatives with none are skipped).</summary>
    private List<Candidate> LoadCandidates(long productId)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT o.Id, o.Name, b.Name, b.QualityTier, o.AverageRating, o.RatingCount, l.Id, m.Name, l.CurrentPrice
            FROM Alternatives a
            JOIN Products o ON o.Id = a.AlternativeProductId
            JOIN Brands b ON b.Id = o.BrandId
            JOIN Listings l ON l.Id = (SELECT l2.Id FROM Listings l2
                                       WHERE l2.ProductId = o.Id AND l2.CurrentPrice IS NOT NULL
                                       ORDER BY l2.CurrentPrice, l2.Id LIMIT 1)
            JOIN Marketplaces m ON m.Id = l.MarketplaceId
            WHERE a.ProductId = $p
            """;
        cmd.Parameters.AddWithValue("$p", productId);

        var list = new List<Candidate>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new Candidate(r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetInt32(3),
                r.IsDBNull(4) ? null : r.GetDouble(4), r.IsDBNull(5) ? null : r.GetInt32(5),
                r.GetInt64(6), r.GetString(7), r.GetDouble(8)));
        return list;
    }
}
