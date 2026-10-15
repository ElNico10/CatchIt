using System.Text;
using CatchIt.Core.Data;

namespace CatchIt.Core.Search;

/// <summary>Optional narrowing of search results by brand quality and seller reputation (stories #10 and #11).</summary>
/// <param name="QualityTier">Only show products from brands of exactly this tier: 1 = Budget, 2 = Standard, 3 = Premium. Null means any.</param>
/// <param name="MinSellerRating">Only show listings from sellers rated at least this, 0 to 5. Null means any.</param>
public record ListingFilters(int? QualityTier = null, double? MinSellerRating = null)
{
    /// <summary>No filtering.</summary>
    public static readonly ListingFilters None = new();

    /// <summary>True when any filter is set.</summary>
    public bool IsActive => QualityTier != null || MinSellerRating != null;
}

/// <summary>One marketplace listing of a product, as shown in the search results.</summary>
/// <param name="ListingId">The listing's id.</param>
/// <param name="ProductId">The product being sold.</param>
/// <param name="ProductName">The product name.</param>
/// <param name="BrandName">The brand that makes it.</param>
/// <param name="QualityTier">The brand's quality tier: 1 = Budget, 2 = Standard, 3 = Premium.</param>
/// <param name="MarketplaceName">The marketplace selling it.</param>
/// <param name="SellerRating">The seller's reputation, 0 to 5. SIMULATED.</param>
/// <param name="CurrentPrice">The current price in US dollars, or <c>null</c> when none is available. SIMULATED.</param>
public record ListingHit(
    long ListingId, long ProductId, string ProductName, string BrandName, int QualityTier,
    string MarketplaceName, double SellerRating, double? CurrentPrice);

/// <summary>One page of listing search results.</summary>
/// <param name="Hits">The listings, cheapest first. Listings without a price come last.</param>
/// <param name="TotalListings">
/// How many listings matched in all (more than <paramref name="Hits"/> if the page was limited).
/// Null for a quick search, which does not count them.
/// </param>
/// <param name="TotalProducts">
/// How many products matched. Null for a full search. For a quick search it stops counting at a cap;
/// see <paramref name="TotalsAreCapped"/>.
/// </param>
/// <param name="TotalsAreCapped">True when <paramref name="TotalProducts"/> stopped at its cap and the real number is larger.</param>
/// <param name="IsComplete">
/// True when the page covers every matching product, so it shows the same listings in the same order
/// the full search would. A full search is always complete. A quick search is complete only when no more
/// than its product limit matched.
/// </param>
/// <param name="TextMatchedAnyProduct">
/// False when the typed text matches no product at all (the screen says "No products found").
/// True when products matched, even if the filters then removed them all ("No products match your filters").
/// </param>
public record ListingSearchPage(
    IReadOnlyList<ListingHit> Hits, long? TotalListings, long? TotalProducts, bool TotalsAreCapped,
    bool IsComplete, bool TextMatchedAnyProduct);

/// <summary>
/// Searches the catalog and returns marketplace listings with their prices, cheapest first
/// (user stories #5, #6, #9, #10, #11). Words are matched exactly as in <see cref="ProductSearchService"/>:
/// every typed word must be the start of a word in the product's name or keywords, ignoring case.
/// </summary>
/// <remarks>
/// Like the product search, there are two modes that agree on which listings match:
/// <list type="bullet">
/// <item><description>
/// <see cref="Search"/> considers every matching product and lists the cheapest listings of all of them.
/// Exact, but it has to look at every match, so a very common word in a huge catalog takes
/// tenths of a second (about 0.2 s at a million products). Use it when the user presses Search.
/// </description></item>
/// <item><description>
/// <see cref="QuickSearch"/> looks only at the first few matching products (50 by default), so it takes
/// a few milliseconds. Use it while the user is still typing. When more products matched than it
/// looked at, <see cref="ListingSearchPage.IsComplete"/> is false and the screen should say so.
/// </description></item>
/// </list>
/// When the text has no searchable words (empty, spaces or punctuation only) there is nothing to match, so
/// both modes browse instead: they list the cheapest listings in the whole catalog that pass the filters.
/// That is how the filters can be used without typing anything. Browsing is exact and fast in both modes.
/// <para>
/// Filters are applied exactly in both modes: a quick search never reports "no results" just because
/// the first products it looked at failed a filter. Each call uses its own connection, so the service is
/// safe to call from several threads.
/// </para>
/// </remarks>
public class ListingSearchService
{
    /// <summary>How many listings a page holds unless told otherwise.</summary>
    public const int DefaultLimit = 100;

    /// <summary>How many matching products a quick search looks at unless told otherwise.</summary>
    public const int DefaultQuickProducts = 50;

    /// <summary>Where a quick search stops counting matching products.</summary>
    public const int DefaultCountCap = 1000;

    private const int MaxLimit = 500;

    // The column list shared by both queries.
    private const string Columns =
        "l.Id, p.Id, p.Name, b.Name, b.QualityTier, m.Name, l.SellerRating, l.CurrentPrice";

    // Cheapest first; listings with no price go last so they can never look like the cheapest.
    // Ties break on the listing id, so equal prices always appear together in a steady order.
    private const string Order = "ORDER BY (l.CurrentPrice IS NULL), l.CurrentPrice, l.Id";

    private readonly Database _db;

    /// <summary>Creates the service.</summary>
    /// <param name="db">The database holding the catalog and its search index.</param>
    public ListingSearchService(Database db)
    {
        _db = db;
    }

    /// <summary>Finds the cheapest listings across every matching product.</summary>
    /// <param name="text">What the user typed. Text with no searchable words (empty, spaces, punctuation) lists the cheapest listings in the whole catalog instead.</param>
    /// <param name="filters">Optional narrowing; null means none.</param>
    /// <param name="limit">How many listings to return at most, 1 to 500.</param>
    /// <exception cref="ArgumentOutOfRangeException">A filter value is out of range.</exception>
    public ListingSearchPage Search(string? text, ListingFilters? filters = null, int limit = DefaultLimit)
    {
        filters ??= ListingFilters.None;
        Validate(filters);
        limit = Math.Clamp(limit, 1, MaxLimit);

        // Nothing searchable typed: browse the whole catalog instead.
        var query = ProductSearchService.BuildQuery(text);
        if (query == null) return Browse(filters, limit);

        using var conn = _db.Open();

        var hits = new List<ListingHit>();
        long total = 0;
        using (var cmd = conn.CreateCommand())
        {
            // COUNT(*) OVER () counts every matching listing, not only the ones kept by LIMIT. The query
            // has to look at all of them to sort anyway, so the count costs almost nothing extra.
            var sql = new StringBuilder($"""
                SELECT {Columns}, COUNT(*) OVER ()
                FROM ProductSearch
                JOIN Products p ON p.Id = ProductSearch.rowid
                JOIN Brands b ON b.Id = p.BrandId
                JOIN Listings l ON l.ProductId = p.Id
                JOIN Marketplaces m ON m.Id = l.MarketplaceId
                WHERE ProductSearch MATCH $q
                """);
            AddListingFilters(sql, filters);
            sql.Append($" {Order} LIMIT $limit");
            cmd.CommandText = sql.ToString();
            cmd.Parameters.AddWithValue("$q", query);
            cmd.Parameters.AddWithValue("$limit", limit);
            BindFilters(cmd, filters);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                hits.Add(ReadHit(reader));
                total = reader.GetInt64(8);
            }
        }

        return new ListingSearchPage(hits, total, null, false, true,
            hits.Count > 0 || TextMatchesAnyProduct(conn, query));
    }

    /// <summary>
    /// A fast search for as-you-type results: only the first few matching products are considered.
    /// </summary>
    /// <param name="text">What the user typed. Text with no searchable words (empty, spaces, punctuation) lists the cheapest listings in the whole catalog instead.</param>
    /// <param name="filters">Optional narrowing; null means none. Applied exactly.</param>
    /// <param name="limit">How many listings to return at most, 1 to 500.</param>
    /// <param name="productLimit">How many matching products to look at, 1 to 500.</param>
    /// <param name="countCap">Stop counting matching products at this many (at least 1).</param>
    /// <exception cref="ArgumentOutOfRangeException">A filter value is out of range.</exception>
    public ListingSearchPage QuickSearch(string? text, ListingFilters? filters = null, int limit = DefaultLimit,
        int productLimit = DefaultQuickProducts, int countCap = DefaultCountCap)
    {
        filters ??= ListingFilters.None;
        Validate(filters);
        limit = Math.Clamp(limit, 1, MaxLimit);

        // Nothing searchable typed: browse the whole catalog instead. This is already exact and fast
        // (it reads listings in price order and stops), so a quick search has nothing to cut short.
        var query = ProductSearchService.BuildQuery(text);
        if (query == null) return Browse(filters, limit);

        productLimit = Math.Clamp(productLimit, 1, MaxLimit);
        countCap = Math.Max(1, countCap);
        using var conn = _db.Open();

        // How many products match (with the filters), counting no further than the cap. This decides
        // whether the quick page already covers everything the full search would show.
        long products;
        using (var count = conn.CreateCommand())
        {
            var sql = new StringBuilder("""
                SELECT COUNT(*) FROM (
                    SELECT 1 FROM ProductSearch
                    JOIN Products p ON p.Id = ProductSearch.rowid
                    JOIN Brands b ON b.Id = p.BrandId
                    WHERE ProductSearch MATCH $q
                """);
            AddProductFilters(sql, filters);
            sql.Append(" LIMIT $probe)");
            count.CommandText = sql.ToString();
            count.Parameters.AddWithValue("$q", query);
            count.Parameters.AddWithValue("$probe", (long)countCap + 1);
            BindFilters(count, filters);
            products = Convert.ToInt64(count.ExecuteScalar());
        }
        var capped = products > countCap;
        if (capped) products = countCap;

        var hits = new List<ListingHit>();
        using (var cmd = conn.CreateCommand())
        {
            // The filters are applied to choose the products (the ones that have at least one listing that
            // passes) and again to pick their listings, so the products looked at are always ones that qualify.
            var first = new StringBuilder("""
                SELECT p.Id AS ProductId FROM ProductSearch
                JOIN Products p ON p.Id = ProductSearch.rowid
                JOIN Brands b ON b.Id = p.BrandId
                WHERE ProductSearch MATCH $q
                """);
            AddProductFilters(first, filters);
            first.Append(" ORDER BY ProductSearch.rowid LIMIT $productLimit");

            var sql = new StringBuilder($"""
                SELECT {Columns}
                FROM ({first}) chosen
                JOIN Products p ON p.Id = chosen.ProductId
                JOIN Brands b ON b.Id = p.BrandId
                JOIN Listings l ON l.ProductId = p.Id
                JOIN Marketplaces m ON m.Id = l.MarketplaceId
                WHERE 1 = 1
                """);
            AddListingFilters(sql, filters, includeTier: false);
            sql.Append($" {Order} LIMIT $limit");

            cmd.CommandText = sql.ToString();
            cmd.Parameters.AddWithValue("$q", query);
            cmd.Parameters.AddWithValue("$productLimit", productLimit);
            cmd.Parameters.AddWithValue("$limit", limit);
            BindFilters(cmd, filters);

            using var reader = cmd.ExecuteReader();
            while (reader.Read()) hits.Add(ReadHit(reader));
        }

        var matchedText = hits.Count > 0 || products > 0 || TextMatchesAnyProduct(conn, query);
        return new ListingSearchPage(hits, null, products, capped, !capped && products <= productLimit, matchedText);
    }

    /// <summary><see cref="Search"/> on a background thread. A cancelled call ends as cancelled and its result is discarded.</summary>
    public Task<ListingSearchPage> SearchAsync(string? text, ListingFilters? filters = null, int limit = DefaultLimit,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => Checked(cancellationToken, () => Search(text, filters, limit)), cancellationToken);

    /// <summary><see cref="QuickSearch"/> on a background thread. See <see cref="SearchAsync"/> for cancellation.</summary>
    public Task<ListingSearchPage> QuickSearchAsync(string? text, ListingFilters? filters = null, int limit = DefaultLimit,
        int productLimit = DefaultQuickProducts, int countCap = DefaultCountCap, CancellationToken cancellationToken = default) =>
        Task.Run(() => Checked(cancellationToken, () => QuickSearch(text, filters, limit, productLimit, countCap)), cancellationToken);

    private static ListingSearchPage Checked(CancellationToken token, Func<ListingSearchPage> search)
    {
        token.ThrowIfCancellationRequested();
        var page = search();
        token.ThrowIfCancellationRequested();
        return page;
    }

    /// <summary>
    /// Lists the cheapest listings in the whole catalog that pass the filters, for when no search text was
    /// typed. Priced listings come first in price order, then unpriced ones.
    /// </summary>
    /// <remarks>
    /// Fast at any size: the listing price index lets the database read listings in price order and
    /// stop once it has enough, rather than sorting every listing. It asks for one row more than
    /// the page holds, which shows whether more exist without counting them all.
    /// </remarks>
    private ListingSearchPage Browse(ListingFilters filters, int limit)
    {
        using var conn = _db.Open();
        var take = limit + 1;

        var hits = ReadBrowse(conn, filters, priced: true, take);
        if (hits.Count < take)
            hits.AddRange(ReadBrowse(conn, filters, priced: false, take - hits.Count));

        // The extra row was only a probe: it proves there are more listings than the page shows.
        var more = hits.Count > limit;
        if (more) hits.RemoveRange(limit, hits.Count - limit);

        // With no text there is nothing for the text to "match", so this is true whenever the catalog has any
        // product at all: it is what tells "no products found" (an empty catalog) from "no products match your filters".
        var catalogHasProducts = hits.Count > 0 || AnyProductExists(conn);

        // If nothing was cut off, the page is everything and its size is the exact total.
        return new ListingSearchPage(hits, more ? null : hits.Count, null, false, true, catalogHasProducts);
    }

    /// <summary>Reads priced listings in price order, or unpriced ones in id order, passing the filters.</summary>
    private static List<ListingHit> ReadBrowse(Microsoft.Data.Sqlite.SqliteConnection conn, ListingFilters filters, bool priced, int take)
    {
        using var cmd = conn.CreateCommand();
        var sql = new StringBuilder($"""
            SELECT {Columns}
            FROM Listings l
            JOIN Products p ON p.Id = l.ProductId
            JOIN Brands b ON b.Id = p.BrandId
            JOIN Marketplaces m ON m.Id = l.MarketplaceId
            WHERE l.CurrentPrice IS {(priced ? "NOT " : "")}NULL
            """);
        AddListingFilters(sql, filters);
        sql.Append(priced ? " ORDER BY l.CurrentPrice, l.Id" : " ORDER BY l.Id");
        sql.Append(" LIMIT $take");
        cmd.CommandText = sql.ToString();
        cmd.Parameters.AddWithValue("$take", take);
        BindFilters(cmd, filters);

        var hits = new List<ListingHit>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) hits.Add(ReadHit(reader));
        return hits;
    }

    private static bool AnyProductExists(Microsoft.Data.Sqlite.SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM Products LIMIT 1";
        return cmd.ExecuteScalar() != null;
    }

    private static void Validate(ListingFilters filters)
    {
        if (filters.QualityTier is < 1 or > 3)
            throw new ArgumentOutOfRangeException(nameof(filters), "The quality tier must be 1, 2 or 3.");
        if (filters.MinSellerRating is { } rating && (double.IsNaN(rating) || rating < 0 || rating > 5))
            throw new ArgumentOutOfRangeException(nameof(filters), "The minimum seller rating must be between 0 and 5.");
    }

    /// <summary>Adds the brand and seller conditions that apply to a single listing row (SQL only; see <see cref="BindFilters"/>).</summary>
    private static void AddListingFilters(StringBuilder sql, ListingFilters filters, bool includeTier = true)
    {
        if (includeTier && filters.QualityTier != null)
            sql.Append(" AND b.QualityTier = $tier");
        if (filters.MinSellerRating != null)
            sql.Append(" AND l.SellerRating >= $minRating");
    }

    /// <summary>
    /// Adds the conditions that decide whether a PRODUCT qualifies: its brand tier, and, for the rating
    /// filter, that at least one of its listings meets the minimum (SQL only; see <see cref="BindFilters"/>).
    /// </summary>
    private static void AddProductFilters(StringBuilder sql, ListingFilters filters)
    {
        if (filters.QualityTier != null)
            sql.Append(" AND b.QualityTier = $tier");
        if (filters.MinSellerRating != null)
            sql.Append(" AND EXISTS (SELECT 1 FROM Listings x WHERE x.ProductId = p.Id AND x.SellerRating >= $minRating)");
    }

    /// <summary>Supplies the values for the filter conditions the helpers above wrote, once per command.</summary>
    private static void BindFilters(Microsoft.Data.Sqlite.SqliteCommand cmd, ListingFilters filters)
    {
        if (filters.QualityTier is { } tier) cmd.Parameters.AddWithValue("$tier", tier);
        if (filters.MinSellerRating is { } rating) cmd.Parameters.AddWithValue("$minRating", rating);
    }

    /// <summary>Checks whether the text matches any product at all, ignoring the filters.</summary>
    private static bool TextMatchesAnyProduct(Microsoft.Data.Sqlite.SqliteConnection conn, string query)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM ProductSearch WHERE ProductSearch MATCH $q LIMIT 1";
        cmd.Parameters.AddWithValue("$q", query);
        return cmd.ExecuteScalar() != null;
    }

    private static ListingHit ReadHit(Microsoft.Data.Sqlite.SqliteDataReader r) =>
        new(r.GetInt64(0), r.GetInt64(1), r.GetString(2), r.GetString(3), r.GetInt32(4),
            r.GetString(5), r.GetDouble(6), r.IsDBNull(7) ? null : r.GetDouble(7));
}
