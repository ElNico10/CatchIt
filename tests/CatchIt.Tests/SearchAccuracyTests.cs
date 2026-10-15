using System.Text;
using CatchIt.Core.Data;
using CatchIt.Core.Search;
using Microsoft.Data.Sqlite;

namespace CatchIt.Tests;

/// <summary>
/// Checks the search against a brute-force "oracle" that does not use the full-text index at all:
/// it reads every product's name and keywords straight from the table and applies the matching
/// rules in plain C# (every typed word must be the start of some word in the name or keywords,
/// ignoring case). Hundreds of random queries are compared, so any disagreement between the
/// index and the rules shows up here.
/// </summary>
/// <remarks>
/// Both modes are held to the same truth: <see cref="ProductSearchService.Search"/> and
/// <see cref="ProductSearchService.QuickSearch"/> must report exactly the products the oracle finds
/// (the quick mode may stop counting at its cap, and shows only a limited number of them).
/// </remarks>
public class SearchAccuracyTests : IDisposable
{
    private const int ProductCount = 10_000;
    private const int QueryCount = 300;
    private const int CountCap = 100;     // low enough that many queries hit the cap
    private const int BigLimit = 500;     // the largest page the service will return

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"catchit-test-{Guid.NewGuid():N}.db");
    private readonly Database _db;
    private readonly ProductSearchService _search;

    // The oracle's copy of the catalog: each product's id and its lower-cased words.
    private readonly List<(long Id, string[] Words)> _catalog = new();

    public SearchAccuracyTests()
    {
        _db = new Database(_path);

        // Names mix a small vocabulary so that many products share words, which makes
        // prefix and multi-word matches overlap in interesting ways.
        string[] brands = { "Apex", "Zenith", "Orbit", "Pioneer", "ValueTech", "Generic", "Nova", "Helix", "Vertex", "Summit" };
        string[] adjectives = { "Wireless", "Portable", "Compact", "Pro", "Ultra", "Smart", "Classic", "Premium", "Budget", "Gaming" };
        string[] types = { "Headphones", "Monitor", "Keyboard", "Mouse", "Speaker", "Webcam", "Router", "Charger", "Laptop", "Tablet", "Printer", "Camera" };
        var rng = new Random(2026);
        var products = Enumerable.Range(0, ProductCount).Select(_ =>
        {
            var brand = brands[rng.Next(brands.Length)];
            var type = types[rng.Next(types.Length)];
            return new ImportedProduct(
                $"{brand} {adjectives[rng.Next(adjectives.Length)]} {type} X-{rng.Next(100, 9999)}",
                brand, 1 + rng.Next(3), $"{type.ToLowerInvariant()} electronics gadget", 10 + rng.Next(400));
        });
        new BulkImporter(_db, historyDays: 7).Import(products);
        _search = new ProductSearchService(_db);

        // Load the oracle's catalog from the table, independent of the index.
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, Keywords FROM Products";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            _catalog.Add((reader.GetInt64(0), Tokenize($"{reader.GetString(1)} {reader.GetString(2)}")));
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
            if (char.IsLetterOrDigit(ch))
            {
                current.Append(char.ToLowerInvariant(ch));
            }
            else if (current.Length > 0)
            {
                words.Add(current.ToString());
                current.Clear();
            }
        }
        if (current.Length > 0) words.Add(current.ToString());
        return words.ToArray();
    }

    /// <summary>The products that match, by the rules, found the slow obvious way.</summary>
    private HashSet<long> Truth(string query)
    {
        var words = Tokenize(query).Take(ProductSearchService.MaxWords)
            .Select(w => w.Length > ProductSearchService.MaxWordLength ? w[..ProductSearchService.MaxWordLength] : w)
            .ToArray();
        if (words.Length == 0) return new HashSet<long>();

        return _catalog
            .Where(p => words.All(w => p.Words.Any(token => token.StartsWith(w, StringComparison.Ordinal))))
            .Select(p => p.Id)
            .ToHashSet();
    }

    /// <summary>Random queries built from real words, cut short at random, plus some fixed awkward ones.</summary>
    private List<string> Queries()
    {
        var rng = new Random(77);
        var queries = new List<string> { "wireless", "headphones pro", "x", "x 1", "a", "1", "12", "zzzz", "gadget electronics", "WIRELESS headphones", "x-1234", "apex-pro" };
        while (queries.Count < QueryCount)
        {
            var product = _catalog[rng.Next(_catalog.Count)];
            var wordCount = rng.Next(1, 4);
            var parts = Enumerable.Range(0, wordCount)
                .Select(_ => product.Words[rng.Next(product.Words.Length)])
                .Select(w => w[..rng.Next(1, w.Length + 1)]);   // cut each word to a random prefix
            var separator = new[] { " ", "  ", ", ", "-" }[rng.Next(4)];
            var query = string.Join(separator, parts);
            queries.Add(rng.Next(5) == 0 ? query.ToUpperInvariant() : query);
        }
        return queries;
    }

    [Fact]
    public void RankedSearch_FindsExactlyTheProductsTheRulesSay()
    {
        foreach (var query in Queries())
        {
            var truth = Truth(query);

            var page = _search.Search(query, limit: 50);

            Assert.True(truth.Count == page.TotalMatches, $"'{query}': expected {truth.Count} matches, search said {page.TotalMatches}");
            Assert.False(page.TotalIsCapped, $"'{query}': the ranked total is always exact");
            Assert.True(page.Hits.Count == Math.Min(50, truth.Count), $"'{query}': expected {Math.Min(50, truth.Count)} hits, got {page.Hits.Count}");
            Assert.True(page.Hits.All(h => truth.Contains(h.ProductId)), $"'{query}': a hit does not match the rules");
            Assert.Equal(page.Hits.Count, page.Hits.Select(h => h.ProductId).Distinct().Count());
        }
    }

    [Fact]
    public void RankedSearch_WhenAllMatchesFitOnThePage_ReturnsEveryOneOfThem()
    {
        var checkedQueries = 0;
        foreach (var query in Queries())
        {
            var truth = Truth(query);
            if (truth.Count == 0 || truth.Count > BigLimit) continue;

            var ids = _search.Search(query, BigLimit).Hits.Select(h => h.ProductId).ToHashSet();

            Assert.True(truth.SetEquals(ids), $"'{query}': the set of results differs from the rules");
            checkedQueries++;
        }
        Assert.True(checkedQueries > 50, "too few queries exercised the full-set check");
    }

    [Fact]
    public void QuickSearch_ShowsOnlyRealMatches_AndCountsExactlyUpToTheCap()
    {
        var capped = 0;
        var exact = 0;
        foreach (var query in Queries())
        {
            var truth = Truth(query);

            var page = _search.QuickSearch(query, limit: 50, countCap: CountCap);

            Assert.True(page.Hits.All(h => truth.Contains(h.ProductId)), $"'{query}': a hit does not match the rules");
            Assert.Equal(page.Hits.Count, page.Hits.Select(h => h.ProductId).Distinct().Count());
            Assert.True(page.Hits.Count == Math.Min(50, truth.Count), $"'{query}': expected {Math.Min(50, truth.Count)} hits, got {page.Hits.Count}");

            if (truth.Count > CountCap)
            {
                Assert.True(page.TotalIsCapped, $"'{query}': {truth.Count} matches should be reported as capped");
                Assert.Equal(CountCap, page.TotalMatches);
                capped++;
            }
            else
            {
                Assert.False(page.TotalIsCapped, $"'{query}': {truth.Count} matches is below the cap, so the total is exact");
                Assert.True(truth.Count == page.TotalMatches, $"'{query}': expected {truth.Count} matches, got {page.TotalMatches}");
                exact++;
            }
        }
        Assert.True(capped > 20 && exact > 20, $"the queries should exercise both branches (capped {capped}, exact {exact})");
    }

    [Fact]
    public void QuickSearch_WhenAllMatchesFitOnThePage_ReturnsEveryOneOfThem()
    {
        var checkedQueries = 0;
        foreach (var query in Queries())
        {
            var truth = Truth(query);
            if (truth.Count == 0 || truth.Count > BigLimit) continue;

            var ids = _search.QuickSearch(query, BigLimit, countCap: 100_000).Hits.Select(h => h.ProductId).ToHashSet();

            Assert.True(truth.SetEquals(ids), $"'{query}': the set of results differs from the rules");
            checkedQueries++;
        }
        Assert.True(checkedQueries > 50, "too few queries exercised the full-set check");
    }

    [Fact]
    public void BothModesAgreeOnWhichProductsMatch()
    {
        foreach (var query in Queries())
        {
            var ranked = _search.Search(query, BigLimit);
            var quick = _search.QuickSearch(query, BigLimit, countCap: 100_000);

            Assert.True(ranked.TotalMatches == quick.TotalMatches, $"'{query}': the modes disagree on the total");
            if (ranked.TotalMatches <= BigLimit)
                Assert.True(ranked.Hits.Select(h => h.ProductId).ToHashSet().SetEquals(quick.Hits.Select(h => h.ProductId)),
                    $"'{query}': the modes returned different products");
        }
    }

    [Fact]
    public void QuickSearch_ListsProductsInASteadyOrder()
    {
        foreach (var query in new[] { "wireless", "x", "a", "headphones pro" })
        {
            var ids = _search.QuickSearch(query, 200).Hits.Select(h => h.ProductId).ToList();

            Assert.Equal(ids.OrderBy(i => i).ToList(), ids);
            Assert.Equal(ids, _search.QuickSearch(query, 200).Hits.Select(h => h.ProductId).ToList()); // and repeatable
        }
    }

    [Fact]
    public void MatchingIgnoresCaseAndPunctuationInTheQuery()
    {
        var plain = _search.Search("wireless headphones", BigLimit);

        Assert.Equal(plain.TotalMatches, _search.Search("WIRELESS   Headphones", BigLimit).TotalMatches);
        Assert.Equal(plain.TotalMatches, _search.Search("wireless, headphones!", BigLimit).TotalMatches);
        Assert.Equal(plain.TotalMatches, _search.QuickSearch("Wireless-Headphones", BigLimit, 100_000).TotalMatches);
    }
}
