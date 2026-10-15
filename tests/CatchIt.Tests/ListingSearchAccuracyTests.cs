using System.Text;
using CatchIt.Core.Data;
using CatchIt.Core.Search;
using Microsoft.Data.Sqlite;

namespace CatchIt.Tests;

/// <summary>
/// Checks <see cref="ListingSearchService"/> against a brute-force "oracle" that never touches the search
/// index or the SQL ordering: it reads every product and listing from the tables and does the matching,
/// filtering and sorting in plain C#. Hundreds of random queries, each with and without filters, must
/// give exactly the listings the oracle says, in exactly its order.
/// </summary>
/// <remarks>
/// A few listings have their price removed, so the "no price goes last" rule is exercised at scale too.
/// </remarks>
public class ListingSearchAccuracyTests : IDisposable
{
    private const int ProductCount = 10_000;
    private const int QueryCount = 250;
    private const int Limit = 100;
    private const int ProductLimit = 50;
    private const int CountCap = 200;   // low enough that many queries hit the cap

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"catchit-test-{Guid.NewGuid():N}.db");
    private readonly Database _db;
    private readonly ListingSearchService _search;

    private record OracleProduct(long Id, string[] Words, int Tier);
    private record OracleListing(long Id, long ProductId, double? Price, double Rating);

    private readonly List<OracleProduct> _products = new();
    private readonly Dictionary<long, List<OracleListing>> _listingsByProduct = new();

    private static readonly ListingFilters[] FilterSets =
    {
        ListingFilters.None,
        new(QualityTier: 3),
        new(QualityTier: 1),
        new(MinSellerRating: 4.0),
        new(MinSellerRating: 4.8),
        new(QualityTier: 2, MinSellerRating: 4.0),
        new(QualityTier: 3, MinSellerRating: 4.9),
    };

    public ListingSearchAccuracyTests()
    {
        _db = new Database(_path);

        string[] brands = { "Apex", "Zenith", "Orbit", "Pioneer", "ValueTech", "Generic", "Nova", "Helix", "Vertex", "Summit" };
        string[] adjectives = { "Wireless", "Portable", "Compact", "Pro", "Ultra", "Smart", "Classic", "Premium", "Budget", "Gaming" };
        string[] types = { "Headphones", "Monitor", "Keyboard", "Mouse", "Speaker", "Webcam", "Router", "Charger", "Laptop", "Tablet", "Printer", "Camera" };
        var rng = new Random(2027);
        var products = Enumerable.Range(0, ProductCount).Select(i =>
        {
            var brandIndex = rng.Next(brands.Length);
            var type = types[rng.Next(types.Length)];
            return new ImportedProduct(
                $"{brands[brandIndex]} {adjectives[rng.Next(adjectives.Length)]} {type} X-{rng.Next(100, 9999)}",
                brands[brandIndex], 1 + brandIndex % 3, $"{type.ToLowerInvariant()} electronics gadget", 10 + rng.Next(400));
        });
        new BulkImporter(_db, historyDays: 7).Import(products);

        using (var conn = _db.Open())
        {
            // Take the price away from every 25th listing, so unpriced listings are mixed in everywhere.
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "UPDATE Listings SET CurrentPrice = NULL WHERE Id % 25 = 0";
                cmd.ExecuteNonQuery();
            }

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT p.Id, p.Name, p.Keywords, b.QualityTier FROM Products p JOIN Brands b ON b.Id = p.BrandId";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    _products.Add(new OracleProduct(reader.GetInt64(0), Tokenize($"{reader.GetString(1)} {reader.GetString(2)}"), reader.GetInt32(3)));
            }

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT Id, ProductId, CurrentPrice, SellerRating FROM Listings";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var listing = new OracleListing(reader.GetInt64(0), reader.GetInt64(1),
                        reader.IsDBNull(2) ? null : reader.GetDouble(2), reader.GetDouble(3));
                    if (!_listingsByProduct.TryGetValue(listing.ProductId, out var list))
                        _listingsByProduct[listing.ProductId] = list = new List<OracleListing>();
                    list.Add(listing);
                }
            }
        }

        _search = new ListingSearchService(_db);
    }

    public void Dispose()
    {
        // SQLite keeps pooled connections open, which would stop the file being deleted.
        SqliteConnection.ClearAllPools();
        if (File.Exists(_path)) File.Delete(_path);
    }

    /// <summary>Splits text into lower-case words made of letters and digits.</summary>
    private static string[] Tokenize(string text)
    {
        var words = new List<string>();
        var current = new StringBuilder();
        foreach (var ch in text)
        {
            if (char.IsLetterOrDigit(ch)) current.Append(char.ToLowerInvariant(ch));
            else if (current.Length > 0) { words.Add(current.ToString()); current.Clear(); }
        }
        if (current.Length > 0) words.Add(current.ToString());
        return words.ToArray();
    }

    /// <summary>True when the text has no searchable words, so the search browses the whole catalog instead.</summary>
    private static bool IsBrowse(string query) => Tokenize(query).Length == 0;

    /// <summary>
    /// Products whose name or keywords match every typed word, in product id order. With no searchable
    /// words there is nothing to match, so every product counts (the catalog is being browsed).
    /// </summary>
    private List<OracleProduct> MatchingProducts(string query)
    {
        var words = Tokenize(query).Take(ProductSearchService.MaxWords)
            .Select(w => w.Length > ProductSearchService.MaxWordLength ? w[..ProductSearchService.MaxWordLength] : w)
            .ToArray();
        if (words.Length == 0) return _products.OrderBy(p => p.Id).ToList();

        return _products
            .Where(p => words.All(w => p.Words.Any(token => token.StartsWith(w, StringComparison.Ordinal))))
            .OrderBy(p => p.Id)
            .ToList();
    }

    private static bool ListingPasses(OracleListing l, ListingFilters f) => f.MinSellerRating is not { } min || l.Rating >= min;

    private static bool ProductPasses(OracleProduct p, IEnumerable<OracleListing> listings, ListingFilters f) =>
        (f.QualityTier is not { } tier || p.Tier == tier) && listings.Any(l => ListingPasses(l, f));

    /// <summary>The cheapest-first order: priced listings by price, then unpriced ones, ties by listing id.</summary>
    private static List<OracleListing> Sorted(IEnumerable<OracleListing> listings) =>
        listings.OrderBy(l => l.Price == null).ThenBy(l => l.Price ?? 0).ThenBy(l => l.Id).ToList();

    private IEnumerable<OracleListing> ListingsOf(OracleProduct p) =>
        _listingsByProduct.TryGetValue(p.Id, out var list) ? list : Enumerable.Empty<OracleListing>();

    /// <summary>Random queries built from real words, cut short at random, plus some fixed awkward ones.</summary>
    private List<string> Queries()
    {
        var rng = new Random(78);
        var queries = new List<string> { "wireless", "headphones pro", "x", "a", "1", "zzzz", "gadget electronics", "WIRELESS headphones", "x-1234", "apex-pro", "speaker", "", "   ", "!!!" };
        while (queries.Count < QueryCount)
        {
            var product = _products[rng.Next(_products.Count)];
            var parts = Enumerable.Range(0, rng.Next(1, 4))
                .Select(_ => product.Words[rng.Next(product.Words.Length)])
                .Select(w => w[..rng.Next(1, w.Length + 1)]);
            queries.Add(string.Join(new[] { " ", "  ", ", ", "-" }[rng.Next(4)], parts));
        }
        return queries;
    }

    private static string Show(ListingFilters f) => $"[tier {f.QualityTier?.ToString() ?? "any"}, rating {f.MinSellerRating?.ToString() ?? "any"}]";

    [Fact]
    public void FullSearch_GivesExactlyTheOraclesListingsInExactlyItsOrder()
    {
        var nonEmpty = 0;
        foreach (var query in Queries())
        foreach (var filters in FilterSets)
        {
            var products = MatchingProducts(query);
            var expected = Sorted(products.Where(p => filters.QualityTier is not { } t || p.Tier == t)
                .SelectMany(ListingsOf).Where(l => ListingPasses(l, filters)));

            var page = _search.Search(query, filters, Limit);

            var label = $"'{query}' {Show(filters)}";
            Assert.True(expected.Take(Limit).Select(l => l.Id).SequenceEqual(page.Hits.Select(h => h.ListingId)), $"{label}: the listings or their order differ");
            // A text search counts every listing. Browsing the whole catalog only states the total when it all fits on the page.
            long? expectedTotal = IsBrowse(query) && expected.Count > Limit ? null : expected.Count;
            Assert.True(expectedTotal == page.TotalListings, $"{label}: expected total {expectedTotal?.ToString() ?? "unstated"}, got {page.TotalListings?.ToString() ?? "unstated"}");
            Assert.True(page.IsComplete && page.TotalProducts == null && !page.TotalsAreCapped, $"{label}: a full search is always complete");
            Assert.True((products.Count > 0) == page.TextMatchedAnyProduct, $"{label}: TextMatchedAnyProduct is wrong");
            if (expected.Count > 0) nonEmpty++;
        }
        Assert.True(nonEmpty > QueryCount * 2, $"too few queries produced results ({nonEmpty})");
    }

    [Fact]
    public void FullSearch_UnpricedListingsAlwaysComeAfterEveryPricedOne()
    {
        foreach (var query in Queries().Take(120))
        {
            var hits = _search.Search(query, ListingFilters.None, 500).Hits;

            var firstUnpriced = hits.ToList().FindIndex(h => h.CurrentPrice == null);
            var priced = firstUnpriced < 0 ? hits : hits.Take(firstUnpriced).ToList();

            if (firstUnpriced >= 0)
                Assert.True(hits.Skip(firstUnpriced).All(h => h.CurrentPrice == null), $"'{query}': a priced listing follows an unpriced one");

            // Among the priced listings, every price is at least as high as the one before it.
            for (var i = 1; i < priced.Count; i++)
                Assert.True(priced[i - 1].CurrentPrice <= priced[i].CurrentPrice, $"'{query}': prices are not in order at position {i}");
        }
    }

    [Fact]
    public void QuickSearch_GivesExactlyTheOraclesChoiceOfProductsAndListings()
    {
        var incomplete = 0;
        var complete = 0;
        foreach (var query in Queries())
        foreach (var filters in FilterSets)
        {
            var qualifying = MatchingProducts(query).Where(p => ProductPasses(p, ListingsOf(p), filters)).ToList();
            var label = $"'{query}' {Show(filters)}";

            // Browsing (no searchable text) is not cut short to the first products: it is the exact cheapest listings overall.
            if (IsBrowse(query))
            {
                var all = Sorted(qualifying.SelectMany(ListingsOf).Where(l => ListingPasses(l, filters)));
                var browsed = _search.QuickSearch(query, filters, Limit, ProductLimit, CountCap);

                Assert.True(all.Take(Limit).Select(l => l.Id).SequenceEqual(browsed.Hits.Select(h => h.ListingId)), $"{label}: the browsed listings or their order differ");
                Assert.True(browsed.IsComplete && browsed.TotalProducts == null && !browsed.TotalsAreCapped, $"{label}: browsing is always complete");
                Assert.True((all.Count > Limit ? null : (long?)all.Count) == browsed.TotalListings, $"{label}: the browse total is wrong");
                Assert.True(browsed.TextMatchedAnyProduct, $"{label}: the catalog has products");
                complete++;
                continue;
            }

            var chosen = qualifying.Take(ProductLimit);
            var expected = Sorted(chosen.SelectMany(ListingsOf).Where(l => ListingPasses(l, filters)));

            var page = _search.QuickSearch(query, filters, Limit, ProductLimit, CountCap);

            Assert.True(expected.Take(Limit).Select(l => l.Id).SequenceEqual(page.Hits.Select(h => h.ListingId)), $"{label}: the listings or their order differ");

            var expectedProducts = Math.Min(qualifying.Count, CountCap);
            Assert.True(expectedProducts == page.TotalProducts, $"{label}: expected {expectedProducts} products, got {page.TotalProducts}");
            Assert.True((qualifying.Count > CountCap) == page.TotalsAreCapped, $"{label}: capped flag is wrong");
            Assert.True((qualifying.Count <= ProductLimit) == page.IsComplete, $"{label}: IsComplete is wrong ({qualifying.Count} qualifying products)");
            Assert.True(page.TotalListings == null, $"{label}: a quick search does not count listings");
            Assert.True((MatchingProducts(query).Count > 0) == page.TextMatchedAnyProduct, $"{label}: TextMatchedAnyProduct is wrong");
            if (page.IsComplete) complete++; else incomplete++;
        }
        Assert.True(complete > 100 && incomplete > 100, $"the queries should exercise both cases (complete {complete}, incomplete {incomplete})");
    }

    [Fact]
    public void WheneverTheQuickSearchIsComplete_ItAgreesExactlyWithTheFullSearch()
    {
        var compared = 0;
        foreach (var query in Queries())
        foreach (var filters in FilterSets)
        {
            var quick = _search.QuickSearch(query, filters, Limit, ProductLimit, CountCap);
            if (!quick.IsComplete) continue;

            var full = _search.Search(query, filters, Limit);

            Assert.True(full.Hits.SequenceEqual(quick.Hits), $"'{query}' {Show(filters)}: a complete quick search must equal the full search");
            Assert.True(full.TextMatchedAnyProduct == quick.TextMatchedAnyProduct, $"'{query}' {Show(filters)}: they disagree about whether anything matched");
            compared++;
        }
        Assert.True(compared > 100, $"too few complete quick searches were compared ({compared})");
    }

    [Fact]
    public void WhenTheQuickSearchIsNotComplete_ItNeverShowsAListingTheFullSearchWouldExclude()
    {
        foreach (var query in Queries().Take(150))
        foreach (var filters in FilterSets)
        {
            var quick = _search.QuickSearch(query, filters, 500, ProductLimit, CountCap);
            if (quick.IsComplete) continue;

            var allowed = _search.Search(query, filters, 500).TotalListings!.Value;
            var everything = _search.Search(query, filters, 500).Hits.Select(h => h.ListingId).ToHashSet();

            // Only checkable when the full page holds every listing; otherwise the cheapest-100 cut hides some.
            if (allowed <= 500)
                Assert.True(quick.Hits.All(h => everything.Contains(h.ListingId)), $"'{query}' {Show(filters)}: the quick search shows a listing that does not match");
        }
    }
}
