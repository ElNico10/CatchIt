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
/// <param name="Hits">The matches, up to the requested limit. Their order depends on the search mode.</param>
/// <param name="TotalMatches">
/// How many products matched. Exact unless <paramref name="TotalIsCapped"/> is true, in which
/// case it is the cap and the real number is at least that large (show it as "1,000+").
/// </param>
/// <param name="TotalIsCapped">True when counting stopped at a cap, so <paramref name="TotalMatches"/> is a minimum.</param>
public record SearchPage(IReadOnlyList<ProductHit> Hits, long TotalMatches, bool TotalIsCapped = false);

/// <summary>
/// Searches the product catalog by name or keyword using the full-text index (user story #5).
/// Every word the user types must match the start of a word in the product's name or keywords,
/// ignoring case, so "wireless head" finds "Apex Wireless Headphones". Typing the middle of a
/// word ("eadphone") does not match; that is the deliberate trade-off for a small, fast index.
/// </summary>
/// <remarks>
/// There are two modes with the same matching rules, so they always agree on which products match:
/// <list type="bullet">
/// <item><description>
/// <see cref="Search"/> returns the best matches first (relevance ranked) with an exact total.
/// Use it when the user presses Enter. Scoring every match makes it slow for very common words
/// in a huge catalog (tens of milliseconds at a million products).
/// </description></item>
/// <item><description>
/// <see cref="QuickSearch"/> skips ranking and caps the count, taking a few milliseconds even
/// at a million products. Use it for suggestions while the user is still typing. It shows
/// real matches but not necessarily the best ones, since the user will keep typing to narrow it.
/// </description></item>
/// </list>
/// The service keeps no state, and every call uses its own database connection, so it is safe to
/// call from several threads. The async methods run the search off the calling thread, which keeps
/// a Windows Forms screen responsive.
/// </remarks>
public class ProductSearchService
{
    /// <summary>Most words of a query that are used; extra words are ignored.</summary>
    public const int MaxWords = 8;

    /// <summary>Longest a single word can be before it is cut short.</summary>
    public const int MaxWordLength = 40;

    /// <summary>The default cap for <see cref="QuickSearch"/>'s count: totals above this show as "1,000+".</summary>
    public const int DefaultCountCap = 1000;

    private const int MaxLimit = 500;

    private readonly Database _db;

    /// <summary>Creates the service.</summary>
    /// <param name="db">The database holding the products and their search index.</param>
    public ProductSearchService(Database db)
    {
        _db = db;
    }

    /// <summary>Searches for products, best matches first, with an exact total.</summary>
    /// <param name="text">What the user typed. Empty or punctuation-only text returns no results.</param>
    /// <param name="limit">How many products to return at most, between 1 and 500.</param>
    /// <returns>The best matches by relevance plus the exact number of matches.</returns>
    public SearchPage Search(string? text, int limit = 50) => Run(text, limit, ranked: true, countCap: null);

    /// <summary>
    /// A fast search for as-you-type suggestions: no relevance ranking, and the count stops at a cap.
    /// </summary>
    /// <param name="text">What the user typed. Empty or punctuation-only text returns no results.</param>
    /// <param name="limit">How many products to return at most, between 1 and 500.</param>
    /// <param name="countCap">Stop counting at this many matches (at least 1).</param>
    /// <returns>
    /// Real matches in a steady order (by product id), and a total that is exact up to
    /// <paramref name="countCap"/>; beyond it, <see cref="SearchPage.TotalIsCapped"/> is true.
    /// </returns>
    public SearchPage QuickSearch(string? text, int limit = 50, int countCap = DefaultCountCap) =>
        Run(text, limit, ranked: false, countCap: Math.Max(1, countCap));

    /// <summary>
    /// <see cref="Search"/> on a background thread, so a screen does not freeze while it runs.
    /// </summary>
    /// <param name="text">What the user typed.</param>
    /// <param name="limit">How many products to return at most.</param>
    /// <param name="cancellationToken">
    /// Cancels the search. A query already running cannot be interrupted, but its result is
    /// discarded and the task ends as cancelled.
    /// </param>
    public Task<SearchPage> SearchAsync(string? text, int limit = 50, CancellationToken cancellationToken = default) =>
        Task.Run(() => Checked(cancellationToken, () => Search(text, limit)), cancellationToken);

    /// <summary><see cref="QuickSearch"/> on a background thread. See <see cref="SearchAsync"/> for cancellation.</summary>
    public Task<SearchPage> QuickSearchAsync(string? text, int limit = 50, int countCap = DefaultCountCap,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => Checked(cancellationToken, () => QuickSearch(text, limit, countCap)), cancellationToken);

    /// <summary>Runs a search, checking for cancellation just before and just after the database work.</summary>
    private static SearchPage Checked(CancellationToken token, Func<SearchPage> search)
    {
        token.ThrowIfCancellationRequested();
        var page = search();
        token.ThrowIfCancellationRequested();
        return page;
    }

    private SearchPage Run(string? text, int limit, bool ranked, int? countCap)
    {
        var query = BuildQuery(text);
        if (query == null)
            return new SearchPage(Array.Empty<ProductHit>(), 0);

        limit = Math.Clamp(limit, 1, MaxLimit);

        using var conn = _db.Open();

        long total;
        var capped = false;
        using (var count = conn.CreateCommand())
        {
            if (countCap is null)
            {
                count.CommandText = "SELECT COUNT(*) FROM ProductSearch WHERE ProductSearch MATCH $q";
                count.Parameters.AddWithValue("$q", query);
                total = Convert.ToInt64(count.ExecuteScalar());
            }
            else
            {
                // Look at one match more than the cap: seeing it proves there are more than the cap
                // without counting them all.
                count.CommandText =
                    "SELECT COUNT(*) FROM (SELECT 1 FROM ProductSearch WHERE ProductSearch MATCH $q LIMIT $probe)";
                count.Parameters.AddWithValue("$q", query);
                count.Parameters.AddWithValue("$probe", (long)countCap.Value + 1);
                var seen = Convert.ToInt64(count.ExecuteScalar());
                capped = seen > countCap.Value;
                total = capped ? countCap.Value : seen;
            }
        }

        var hits = new List<ProductHit>();
        using (var cmd = conn.CreateCommand())
        {
            // "rank" is the index's relevance score (best first). Scoring every match is what makes the
            // ranked order slow; the quick order by rowid is how the index already stores matches.
            var order = ranked ? "ProductSearch.rank" : "ProductSearch.rowid";
            cmd.CommandText = $"""
                SELECT p.Id, p.Name, b.Name, b.QualityTier
                FROM ProductSearch
                JOIN Products p ON p.Id = ProductSearch.rowid
                JOIN Brands b ON b.Id = p.BrandId
                WHERE ProductSearch MATCH $q
                ORDER BY {order}
                LIMIT $limit
                """;
            cmd.Parameters.AddWithValue("$q", query);
            cmd.Parameters.AddWithValue("$limit", limit);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                hits.Add(new ProductHit(reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetInt32(3)));
        }

        return new SearchPage(hits, total, capped);
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
