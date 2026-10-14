using System.Text;
using CatchIt.Core.Data;

namespace CatchIt.Core.Search;

/// <summary>One product found by a search.</summary>
/// <param name="ProductId">The product's id.</param>
/// <param name="Name">The product name.</param>
/// <param name="BrandName">The brand that makes it.</param>
/// <param name="QualityTier">The brand's quality tier: 1 = Budget, 2 = Standard, 3 = Premium.</param>
public record ProductHit(long ProductId, string Name, string BrandName, int QualityTier);

/// <summary>One page of search results.</summary>
/// <param name="Hits">The best matches, most relevant first, up to the requested limit.</param>
/// <param name="TotalMatches">How many products matched in all, which can be more than were returned.</param>
public record SearchPage(IReadOnlyList<ProductHit> Hits, long TotalMatches);

/// <summary>
/// Searches the product catalog by name or keyword using the full-text index (user story #5).
/// Every word the user types must match the start of a word in the product's name or keywords,
/// ignoring case, so "wireless head" finds "Apex Wireless Headphones". Typing the middle of a
/// word ("eadphone") does not match; that is the deliberate trade-off for a small, fast index.
/// </summary>
public class ProductSearchService
{
    /// <summary>Most words of a query that are used; extra words are ignored.</summary>
    public const int MaxWords = 8;

    /// <summary>Longest a single word can be before it is cut short.</summary>
    public const int MaxWordLength = 40;

    private const int MaxLimit = 500;

    private readonly Database _db;

    /// <summary>Creates the service.</summary>
    /// <param name="db">The database holding the products and their search index.</param>
    public ProductSearchService(Database db)
    {
        _db = db;
    }

    /// <summary>Searches for products.</summary>
    /// <param name="text">What the user typed. Empty or punctuation-only text returns no results.</param>
    /// <param name="limit">How many products to return at most, between 1 and 500.</param>
    /// <returns>The best matches plus the total number of matches.</returns>
    public SearchPage Search(string? text, int limit = 50)
    {
        var query = BuildQuery(text);
        if (query == null)
            return new SearchPage(Array.Empty<ProductHit>(), 0);

        limit = Math.Clamp(limit, 1, MaxLimit);

        using var conn = _db.Open();

        long total;
        using (var count = conn.CreateCommand())
        {
            count.CommandText = "SELECT COUNT(*) FROM ProductSearch WHERE ProductSearch MATCH $q";
            count.Parameters.AddWithValue("$q", query);
            total = Convert.ToInt64(count.ExecuteScalar());
        }

        var hits = new List<ProductHit>();
        using (var cmd = conn.CreateCommand())
        {
            // "rank" is the index's relevance score (best matches sort first).
            cmd.CommandText = """
                SELECT p.Id, p.Name, b.Name, b.QualityTier
                FROM ProductSearch
                JOIN Products p ON p.Id = ProductSearch.rowid
                JOIN Brands b ON b.Id = p.BrandId
                WHERE ProductSearch MATCH $q
                ORDER BY ProductSearch.rank
                LIMIT $limit
                """;
            cmd.Parameters.AddWithValue("$q", query);
            cmd.Parameters.AddWithValue("$limit", limit);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                hits.Add(new ProductHit(reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetInt32(3)));
        }

        return new SearchPage(hits, total);
    }

    /// <summary>
    /// Turns what the user typed into a safe full-text query: only letters and digits are kept,
    /// and each word becomes a quoted prefix match, e.g. <c>"wireless"* "head"*</c>.
    /// </summary>
    /// <remarks>
    /// The text is never passed to the index as written. The index has its own query language
    /// (AND, OR, NEAR, column filters, quotes), and raw user input could crash a search or
    /// change what it means. Quoting every word makes all of that plain text.
    /// </remarks>
    /// <param name="text">What the user typed.</param>
    /// <returns>The query, or <c>null</c> when the text contains no searchable words.</returns>
    public static string? BuildQuery(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var words = new List<string>();
        var word = new StringBuilder();

        void EndWord()
        {
            if (word.Length > 0 && words.Count < MaxWords)
                words.Add(word.Length > MaxWordLength ? word.ToString(0, MaxWordLength) : word.ToString());
            word.Clear();
        }

        foreach (var ch in text)
        {
            if (char.IsLetterOrDigit(ch)) word.Append(ch);
            else EndWord();
        }
        EndWord();

        return words.Count == 0 ? null : string.Join(" ", words.Select(w => $"\"{w}\"*"));
    }
}
