using CatchIt.Core.Data;
using CatchIt.Core.Search;
using Microsoft.Data.Sqlite;

namespace CatchIt.Tests;

/// <summary>
/// Tests for <see cref="ListingSearchService"/> on small, hand-made catalogs where the right answer
/// can be worked out by eye (user stories #5, #6, #9, #10, #11). Each test gets its own throwaway database file.
/// </summary>
public class ListingSearchServiceTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"catchit-test-{Guid.NewGuid():N}.db");
    private readonly Database _db;
    private readonly ListingSearchService _search;

    public ListingSearchServiceTests()
    {
        _db = new Database(_path);
        _db.Initialize();
        _search = new ListingSearchService(_db);
    }

    public void Dispose()
    {
        // SQLite keeps pooled connections open, which would stop the file being deleted.
        SqliteConnection.ClearAllPools();
        if (File.Exists(_path)) File.Delete(_path);
    }

    /// <summary>Adds a product with the given listings, each as (marketplace, price, seller rating), and returns the listing ids.</summary>
    private long[] AddProduct(string name, string brand, int tier, string keywords, params (string Market, double? Price, double Rating)[] listings)
    {
        var product = TestCatalog.AddProduct(_db, name, brand, tier, keywords);
        return listings.Select(l => TestCatalog.AddListing(_db, product, l.Market, l.Price, l.Rating)).ToArray();
    }

    private static List<string> Describe(ListingSearchPage page) =>
        page.Hits.Select(h => $"{h.ProductName} @ {h.MarketplaceName} {(h.CurrentPrice is { } p ? p.ToString("F2") : "n/a")}").ToList();

    // ---- Ordering (story #9) ----

    [Fact]
    public void Search_ListsListingsCheapestFirst_AcrossProductsAndMarketplaces()
    {
        AddProduct("Apex Wireless Headphones", "Apex", 3, "headphones", ("ShopMart", 180, 4.5), ("BargainBay", 150, 3.0));
        AddProduct("Orbit Wireless Headphones", "Orbit", 2, "headphones", ("ShopMart", 95, 4.0), ("MegaStore", 120, 4.8));
        AddProduct("ValueTech Wireless Headphones", "ValueTech", 1, "headphones", ("QuickBuy", 35, 2.5));

        var page = _search.Search("wireless headphones");

        Assert.Equal(new[]
        {
            "ValueTech Wireless Headphones @ QuickBuy 35.00",
            "Orbit Wireless Headphones @ ShopMart 95.00",
            "Orbit Wireless Headphones @ MegaStore 120.00",
            "Apex Wireless Headphones @ BargainBay 150.00",
            "Apex Wireless Headphones @ ShopMart 180.00",
        }, Describe(page));
    }

    [Fact]
    public void Search_ASingleProductOnSeveralMarketplaces_ListsTheCheapestMarketplaceFirst()
    {
        AddProduct("Zenith Monitor", "Zenith", 3, "monitor", ("A", 420, 4), ("B", 380, 4), ("C", 399, 4));

        var prices = _search.Search("monitor").Hits.Select(h => h.CurrentPrice).ToList();

        Assert.Equal(new double?[] { 380, 399, 420 }, prices);
    }

    [Fact]
    public void Search_TiedLowestPrices_AllAppearAtTheTop_InASteadyOrder()
    {
        var a = AddProduct("Tie Gadget One", "G", 2, "gadget", ("ShopMart", 20, 4));
        var b = AddProduct("Tie Gadget Two", "G", 2, "gadget", ("BargainBay", 20, 4));
        AddProduct("Tie Gadget Three", "G", 2, "gadget", ("MegaStore", 25, 4));

        var page = _search.Search("gadget");

        Assert.Equal(new[] { a[0], b[0] }, page.Hits.Take(2).Select(h => h.ListingId)); // both at the top, by listing id
        Assert.Equal(25, page.Hits[2].CurrentPrice);
        Assert.Equal(page.Hits.Select(h => h.ListingId), _search.Search("gadget").Hits.Select(h => h.ListingId));
    }

    [Fact]
    public void Search_AListingWithoutAPriceComesLast_NeverFirst()
    {
        AddProduct("Pricey Gadget", "G", 2, "gadget", ("A", 999, 4));
        var missing = AddProduct("Unpriced Gadget", "G", 2, "gadget", ("B", null, 4));
        AddProduct("Cheap Gadget", "G", 2, "gadget", ("C", 5, 4));

        var hits = _search.Search("gadget").Hits;

        Assert.Equal(new double?[] { 5, 999, null }, hits.Select(h => h.CurrentPrice));
        Assert.Equal(missing[0], hits[^1].ListingId);
    }

    [Fact]
    public void Search_ReturnsEveryFieldTheScreenNeeds()
    {
        AddProduct("Zenith 4K Monitor", "Zenith", 3, "monitor", ("MegaStore", 420.5, 4.3));

        var hit = _search.Search("monitor").Hits.Single();

        Assert.Equal("Zenith 4K Monitor", hit.ProductName);
        Assert.Equal("Zenith", hit.BrandName);
        Assert.Equal(3, hit.QualityTier);
        Assert.Equal("MegaStore", hit.MarketplaceName);
        Assert.Equal(4.3, hit.SellerRating);
        Assert.Equal(420.5, hit.CurrentPrice);
        Assert.True(hit.ListingId > 0 && hit.ProductId > 0);
    }

    // ---- Matching and the empty cases (stories #5 and #10) ----

    [Fact]
    public void Search_NoMatchingProduct_SaysTheTextMatchedNothing()
    {
        AddProduct("Zenith Monitor", "Zenith", 3, "monitor", ("A", 400, 4));

        var page = _search.Search("refrigerator");

        Assert.Empty(page.Hits);
        Assert.False(page.TextMatchedAnyProduct);
        Assert.Equal(0, page.TotalListings);
    }

    [Fact]
    public void Search_AProductWithNoListings_MatchesTheTextButShowsNothing()
    {
        TestCatalog.AddProduct(_db, "Unlisted Gadget", "G", 2, "gadget");

        var page = _search.Search("gadget");

        Assert.Empty(page.Hits);
        Assert.True(page.TextMatchedAnyProduct); // the screen can tell this apart from "no products found"
    }

    [Fact]
    public void Search_UsesTheSameWordMatchingAsTheProductSearch()
    {
        AddProduct("Apex Wireless Headphones", "Apex", 3, "headphones audio", ("A", 100, 4));

        Assert.Single(_search.Search("WIRELESS head").Hits);           // case and word starts
        Assert.Single(_search.Search("audio").Hits);                    // keywords
        Assert.Empty(_search.Search("eadphone").Hits);                  // not the middle of a word
        Assert.Empty(_search.Search("wireless monitor").Hits);          // every word must match
    }

    [Theory]
    [InlineData("wireless AND")]
    [InlineData("NEAR(wireless headphones)")]
    [InlineData("name:wireless")]
    [InlineData("\"unclosed quote wireless")]
    [InlineData("wireless); DROP TABLE Products; --")]
    public void Search_SearchSyntaxTypedByTheUser_NeverThrows(string text)
    {
        AddProduct("Apex Wireless Headphones", "Apex", 3, "headphones", ("A", 100, 4));

        Assert.Null(Record.Exception(() => _search.Search(text)));
        Assert.Null(Record.Exception(() => _search.QuickSearch(text)));
        Assert.Equal(1, TestCatalog.Count(_db, "SELECT COUNT(*) FROM Products"));
    }

    // ---- Filters (stories #10 and #11) ----

    private void AddFilterCatalog()
    {
        AddProduct("Apex Premium Speaker", "Apex", 3, "speaker", ("A", 200, 4.5), ("B", 180, 3.0));
        AddProduct("Orbit Standard Speaker", "Orbit", 2, "speaker", ("A", 90, 4.0));
        AddProduct("ValueTech Budget Speaker", "ValueTech", 1, "speaker", ("A", 25, 2.0), ("B", 30, 4.9));
    }

    [Fact]
    public void Filter_ByQualityTier_ShowsOnlyThatTier()
    {
        AddFilterCatalog();

        var hits = _search.Search("speaker", new ListingFilters(QualityTier: 3)).Hits;

        Assert.All(hits, h => Assert.Equal(3, h.QualityTier));
        Assert.Equal(2, hits.Count);
    }

    [Fact]
    public void Filter_ByMinimumSellerRating_ShowsOnlyListingsFromSellersAtOrAboveIt()
    {
        AddFilterCatalog();

        var hits = _search.Search("speaker", new ListingFilters(MinSellerRating: 4)).Hits;

        Assert.All(hits, h => Assert.True(h.SellerRating >= 4));
        Assert.Equal(new[] { 4.9, 4.0, 4.5 }, hits.Select(h => h.SellerRating)); // 30, 90, 200 dollars
    }

    [Fact]
    public void Filter_TheRatingBoundaryIsInclusive()
    {
        AddProduct("Edge Gadget", "G", 2, "gadget", ("A", 10, 4.0), ("B", 11, 3.9));

        var hits = _search.Search("gadget", new ListingFilters(MinSellerRating: 4.0)).Hits;

        Assert.Equal(new[] { 4.0 }, hits.Select(h => h.SellerRating));
    }

    [Fact]
    public void Filter_BothTogether_MustPassBoth()
    {
        AddFilterCatalog();

        var hits = _search.Search("speaker", new ListingFilters(QualityTier: 1, MinSellerRating: 4)).Hits;

        Assert.Equal("ValueTech Budget Speaker", hits.Single().ProductName);
        Assert.Equal(4.9, hits.Single().SellerRating);
    }

    [Fact]
    public void Filter_ThatRemovesEverything_SaysProductsMatchedButNoneSurvived()
    {
        AddFilterCatalog();

        var page = _search.Search("speaker", new ListingFilters(QualityTier: 3, MinSellerRating: 4.8));

        Assert.Empty(page.Hits);
        Assert.True(page.TextMatchedAnyProduct); // "No products match your filters", not "No products found"
    }

    [Fact]
    public void Filter_ClearingItBringsEverythingBack()
    {
        AddFilterCatalog();
        var filtered = _search.Search("speaker", new ListingFilters(QualityTier: 3));

        var cleared = _search.Search("speaker", ListingFilters.None);

        Assert.Equal(2, filtered.Hits.Count);
        Assert.Equal(5, cleared.Hits.Count);
        Assert.Equal(cleared.Hits.Count, _search.Search("speaker", null).Hits.Count);
    }

    [Fact]
    public void Filter_IsActive_ReflectsWhetherAnythingIsSet()
    {
        Assert.False(ListingFilters.None.IsActive);
        Assert.True(new ListingFilters(QualityTier: 2).IsActive);
        Assert.True(new ListingFilters(MinSellerRating: 3).IsActive);
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(4, null)]
    [InlineData(-1, null)]
    [InlineData(null, -0.1)]
    [InlineData(null, 5.1)]
    [InlineData(null, double.NaN)]
    public void Filter_OutOfRangeValues_AreRejected(int? tier, double? rating)
    {
        var filters = new ListingFilters(tier, rating);

        Assert.Throws<ArgumentOutOfRangeException>(() => _search.Search("speaker", filters));
        Assert.Throws<ArgumentOutOfRangeException>(() => _search.QuickSearch("speaker", filters));
    }

    // ---- Page size and totals ----

    [Fact]
    public void Search_LimitsTheListingsButCountsThemAll()
    {
        for (var i = 0; i < 12; i++)
            AddProduct($"Counted Gadget {i}", "G", 2, "gadget", ("A", 10 + i, 4), ("B", 50 + i, 4));

        var page = _search.Search("gadget", limit: 5);

        Assert.Equal(5, page.Hits.Count);
        Assert.Equal(24, page.TotalListings);
        Assert.Null(page.TotalProducts);
        Assert.True(page.IsComplete);
        Assert.Equal(new double?[] { 10, 11, 12, 13, 14 }, page.Hits.Select(h => h.CurrentPrice)); // the cheapest five of all
    }

    [Fact]
    public void Search_LimitIsKeptWithinSensibleBounds()
    {
        for (var i = 0; i < 4; i++)
            AddProduct($"Bounded Gadget {i}", "G", 2, "gadget", ("A", 10 + i, 4));

        Assert.Single(_search.Search("gadget", limit: 0).Hits);
        Assert.Single(_search.Search("gadget", limit: -5).Hits);
        Assert.Equal(4, _search.Search("gadget", limit: 100000).Hits.Count);
    }

    // ---- Quick mode ----

    [Fact]
    public void QuickSearch_WhenEveryMatchIsConsidered_GivesTheSameListingsInTheSameOrderAsTheFullSearch()
    {
        AddFilterCatalog();

        var quick = _search.QuickSearch("speaker");
        var full = _search.Search("speaker");

        Assert.True(quick.IsComplete);
        Assert.Equal(full.Hits, quick.Hits);
        Assert.Equal(3, quick.TotalProducts);
        Assert.False(quick.TotalsAreCapped);
        Assert.Null(quick.TotalListings);
    }

    [Fact]
    public void QuickSearch_LooksOnlyAtTheFirstProducts_AndSaysItIsNotComplete()
    {
        for (var i = 0; i < 8; i++)
            AddProduct($"Many Gadget {i}", "G", 2, "gadget", ("A", 100 - i, 4)); // later products are cheaper

        var page = _search.QuickSearch("gadget", productLimit: 3);

        Assert.False(page.IsComplete);
        Assert.Equal(3, page.Hits.Select(h => h.ProductId).Distinct().Count());
        Assert.Equal(8, page.TotalProducts);
        Assert.Equal(new[] { "Many Gadget 2", "Many Gadget 1", "Many Gadget 0" }, page.Hits.Select(h => h.ProductName)); // cheapest of the first three
        Assert.NotEqual(_search.Search("gadget").Hits.First().ProductName, page.Hits.First().ProductName); // the full search finds the real cheapest
    }

    [Fact]
    public void QuickSearch_TheProductCountStopsAtItsCap()
    {
        for (var i = 0; i < 20; i++)
            AddProduct($"Capped Gadget {i}", "G", 2, "gadget", ("A", 10 + i, 4));

        var page = _search.QuickSearch("gadget", productLimit: 5, countCap: 12);

        Assert.Equal(12, page.TotalProducts);
        Assert.True(page.TotalsAreCapped);
        Assert.False(page.IsComplete);
    }

    [Fact]
    public void QuickSearch_ExactlyAsManyProductsAsTheLimit_IsComplete()
    {
        for (var i = 0; i < 5; i++)
            AddProduct($"Exact Gadget {i}", "G", 2, "gadget", ("A", 10 + i, 4));

        Assert.True(_search.QuickSearch("gadget", productLimit: 5).IsComplete);
        Assert.False(_search.QuickSearch("gadget", productLimit: 4).IsComplete);
    }

    [Fact]
    public void QuickSearch_FiltersChooseTheProductsToLookAt_SoItNeverMissesQualifyingOnes()
    {
        // Sixty products. The first fifty-five are Budget; only the last five are Premium.
        for (var i = 0; i < 55; i++)
            AddProduct($"Early Gadget {i}", "BudgetCo", 1, "gadget", ("A", 10 + i, 4));
        for (var i = 0; i < 5; i++)
            AddProduct($"Late Gadget {i}", "PremiumCo", 3, "gadget", ("A", 300 + i, 4));

        var page = _search.QuickSearch("gadget", new ListingFilters(QualityTier: 3), productLimit: 50);

        // A quick search that took the first 50 products and filtered afterwards would find nothing here.
        Assert.Equal(5, page.Hits.Count);
        Assert.All(page.Hits, h => Assert.Equal(3, h.QualityTier));
        Assert.True(page.TextMatchedAnyProduct);
        Assert.True(page.IsComplete);
    }

    [Fact]
    public void QuickSearch_TheRatingFilterAlsoChoosesTheProducts()
    {
        for (var i = 0; i < 55; i++)
            AddProduct($"Poor Gadget {i}", "G", 2, "gadget", ("A", 10 + i, 2.0));
        for (var i = 0; i < 3; i++)
            AddProduct($"Trusted Gadget {i}", "G", 2, "gadget", ("A", 500 + i, 4.5));

        var page = _search.QuickSearch("gadget", new ListingFilters(MinSellerRating: 4), productLimit: 50);

        Assert.Equal(3, page.Hits.Count);
        Assert.All(page.Hits, h => Assert.True(h.SellerRating >= 4));
        Assert.True(page.IsComplete);
    }

    [Fact]
    public void QuickSearch_AProductWithMixedSellers_ShowsOnlyTheListingsThatPassTheRatingFilter()
    {
        AddProduct("Mixed Gadget", "G", 2, "gadget", ("A", 10, 2.0), ("B", 20, 4.5));

        var quick = _search.QuickSearch("gadget", new ListingFilters(MinSellerRating: 4)).Hits;
        var full = _search.Search("gadget", new ListingFilters(MinSellerRating: 4)).Hits;

        Assert.Equal(20, quick.Single().CurrentPrice); // the cheaper listing fails the filter, so it is not shown
        Assert.Equal(full, quick);
    }

    [Fact]
    public void QuickSearch_FilterThatRemovesEverything_StillSaysProductsMatched()
    {
        AddFilterCatalog();

        var page = _search.QuickSearch("speaker", new ListingFilters(QualityTier: 3, MinSellerRating: 4.8));

        Assert.Empty(page.Hits);
        Assert.True(page.TextMatchedAnyProduct);
        Assert.Equal(0, page.TotalProducts);
    }

    [Fact]
    public void QuickSearch_NoMatchingProduct_SaysTheTextMatchedNothing()
    {
        AddFilterCatalog();

        var page = _search.QuickSearch("refrigerator");

        Assert.Empty(page.Hits);
        Assert.False(page.TextMatchedAnyProduct);
    }

    // ---- Browsing: no search text lists the whole catalog, so filters work on their own ----

    private void AddBrowseCatalog()
    {
        AddProduct("Zenith Monitor", "Zenith", 3, "monitor", ("A", 420, 4.5), ("B", 380, 3.0));
        AddProduct("Orbit Speaker", "Orbit", 2, "speaker", ("A", 95, 4.0), ("C", 120, 2.0));
        AddProduct("ValueTech Mouse", "ValueTech", 1, "mouse", ("B", 12, 4.9), ("C", null, 4.2));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!!!")]
    [InlineData("?, .-")]
    public void Browse_TextWithNoSearchableWords_ListsEveryListingCheapestFirst(string? text)
    {
        AddBrowseCatalog();

        foreach (var page in new[] { _search.Search(text), _search.QuickSearch(text) })
        {
            Assert.Equal(new[]
            {
                "ValueTech Mouse @ B 12.00",
                "Orbit Speaker @ A 95.00",
                "Orbit Speaker @ C 120.00",
                "Zenith Monitor @ B 380.00",
                "Zenith Monitor @ A 420.00",
                "ValueTech Mouse @ C n/a",        // no price, so last
            }, Describe(page));
            Assert.True(page.TextMatchedAnyProduct);
            Assert.True(page.IsComplete);
        }
    }

    [Fact]
    public void Browse_QuickAndFullGiveTheSamePage()
    {
        AddBrowseCatalog();

        var full = _search.Search("");
        var quick = _search.QuickSearch("");

        Assert.Equal(full.Hits, quick.Hits);
        Assert.Equal(full.TotalListings, quick.TotalListings);
        Assert.Equal(full.IsComplete, quick.IsComplete);
        Assert.Null(full.TotalProducts);
        Assert.False(full.TotalsAreCapped);
    }

    [Fact]
    public void Browse_FiltersWorkWithoutAnyText()
    {
        AddBrowseCatalog();

        var premium = _search.Search("", new ListingFilters(QualityTier: 3));
        var trusted = _search.Search("", new ListingFilters(MinSellerRating: 4.0));
        var both = _search.Search("", new ListingFilters(QualityTier: 1, MinSellerRating: 4.5));

        Assert.Equal(new[] { "Zenith Monitor @ B 380.00", "Zenith Monitor @ A 420.00" }, Describe(premium));
        Assert.Equal(new[] { "ValueTech Mouse @ B 12.00", "Orbit Speaker @ A 95.00", "Zenith Monitor @ A 420.00", "ValueTech Mouse @ C n/a" }, Describe(trusted));
        Assert.Equal(new[] { "ValueTech Mouse @ B 12.00" }, Describe(both));
    }

    [Fact]
    public void Browse_QuickSearchAppliesTheFiltersTheSameWay()
    {
        AddBrowseCatalog();
        var filters = new ListingFilters(QualityTier: 2, MinSellerRating: 3.5);

        Assert.Equal(_search.Search("", filters).Hits, _search.QuickSearch("", filters).Hits);
        Assert.Equal(new[] { "Orbit Speaker @ A 95.00" }, Describe(_search.QuickSearch("", filters)));
    }

    [Fact]
    public void Browse_UnpricedListingsStayLast_EvenWhenFiltered()
    {
        AddBrowseCatalog();

        var page = _search.Search("", new ListingFilters(MinSellerRating: 4.2));

        Assert.Equal(new double?[] { 12, 420, null }, page.Hits.Select(h => h.CurrentPrice));
    }

    [Fact]
    public void Browse_TiedPricesAppearTogether_InASteadyOrder()
    {
        var a = AddProduct("Tie One", "G", 2, "x", ("A", 20, 4));
        var b = AddProduct("Tie Two", "G", 2, "y", ("B", 20, 4));
        AddProduct("Tie Three", "G", 2, "z", ("C", 30, 4));

        var ids = _search.Search("").Hits.Select(h => h.ListingId).ToList();

        Assert.Equal(new[] { a[0], b[0] }, ids.Take(2));
        Assert.Equal(ids, _search.Search("").Hits.Select(h => h.ListingId).ToList());
    }

    [Fact]
    public void Browse_ShowsTheCheapestOfAllListings_NotJustTheFirstOnes()
    {
        for (var i = 0; i < 30; i++)
            AddProduct($"Browse Gadget {i}", "G", 2, "gadget", ("A", 100 - i, 4)); // later products are cheaper

        var page = _search.Search("", limit: 5);

        Assert.Equal(new double?[] { 71, 72, 73, 74, 75 }, page.Hits.Select(h => h.CurrentPrice));
    }

    [Fact]
    public void Browse_TheTotalIsExactWhenEverythingFits_AndUnknownWhenMoreExist()
    {
        for (var i = 0; i < 10; i++)
            AddProduct($"Counted Gadget {i}", "G", 2, "gadget", ("A", 10 + i, 4));

        Assert.Equal(10, _search.Search("", limit: 10).TotalListings);   // exactly a full page: nothing was left over
        Assert.Equal(10, _search.Search("", limit: 11).TotalListings);   // room to spare
        Assert.Null(_search.Search("", limit: 9).TotalListings);         // one listing did not fit, so the total is not stated
        Assert.Equal(9, _search.Search("", limit: 9).Hits.Count);
    }

    [Fact]
    public void Browse_AnEmptyCatalog_FindsNothing_AndSaysThereAreNoProducts()
    {
        var page = _search.Search("");

        Assert.Empty(page.Hits);
        Assert.False(page.TextMatchedAnyProduct);   // the screen says "No products found"
        Assert.Equal(0, page.TotalListings);
    }

    [Fact]
    public void Browse_FiltersThatRemoveEverything_SayProductsExistButNoneMatch()
    {
        AddBrowseCatalog();

        var page = _search.Search("", new ListingFilters(QualityTier: 3, MinSellerRating: 4.9));

        Assert.Empty(page.Hits);
        Assert.True(page.TextMatchedAnyProduct);    // "No products match your filters"
    }

    [Fact]
    public void Browse_AProductWithNoListingsIsNotShown()
    {
        TestCatalog.AddProduct(_db, "Unlisted Gadget", "G", 2, "gadget");
        AddProduct("Listed Gadget", "G", 2, "gadget", ("A", 10, 4));

        Assert.Equal(new[] { "Listed Gadget @ A 10.00" }, Describe(_search.Search("")));
    }

    [Fact]
    public void Browse_InvalidFiltersAreStillRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _search.Search("", new ListingFilters(QualityTier: 9)));
        Assert.Throws<ArgumentOutOfRangeException>(() => _search.QuickSearch("", new ListingFilters(MinSellerRating: 6)));
    }

    [Fact]
    public async Task Browse_TheAsyncSearchesGiveTheSameResult()
    {
        AddBrowseCatalog();
        var filters = new ListingFilters(MinSellerRating: 4.0);

        Assert.Equal(_search.Search("", filters).Hits, (await _search.SearchAsync("", filters)).Hits);
        Assert.Equal(_search.QuickSearch("", filters).Hits, (await _search.QuickSearchAsync("", filters)).Hits);
    }

    [Fact]
    public void Browse_TypingTextAndThenClearingItGoesBackToTheWholeCatalog()
    {
        AddBrowseCatalog();

        Assert.Single(_search.Search("monitor").Hits.Select(h => h.ProductId).Distinct());
        Assert.Equal(6, _search.Search("").Hits.Count);
    }

    // ---- Async and sharing between threads ----

    [Fact]
    public async Task TheAsyncSearches_GiveTheSameResultsAsTheSyncOnes()
    {
        AddFilterCatalog();
        var filters = new ListingFilters(MinSellerRating: 4);

        Assert.Equal(_search.Search("speaker", filters).Hits, (await _search.SearchAsync("speaker", filters)).Hits);
        Assert.Equal(_search.QuickSearch("speaker", filters).Hits, (await _search.QuickSearchAsync("speaker", filters)).Hits);
    }

    [Fact]
    public async Task TheAsyncSearches_AnAlreadyCancelledToken_IsCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _search.SearchAsync("speaker", cancellationToken: cts.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _search.QuickSearchAsync("speaker", cancellationToken: cts.Token));
    }

    [Fact]
    public async Task ManySearchesAtOnce_AllGetTheSameCorrectAnswer()
    {
        AddFilterCatalog();
        var expectedFull = _search.Search("speaker").Hits.Select(h => h.ListingId).ToList();
        var expectedQuick = _search.QuickSearch("speaker", new ListingFilters(QualityTier: 1)).Hits.Select(h => h.ListingId).ToList();

        var results = await Task.WhenAll(Enumerable.Range(0, 24).Select(i => Task.Run(() => i % 2 == 0
            ? (Kind: "full", Ids: _search.Search("speaker").Hits.Select(h => h.ListingId).ToList())
            : (Kind: "quick", Ids: _search.QuickSearch("speaker", new ListingFilters(QualityTier: 1)).Hits.Select(h => h.ListingId).ToList()))));

        Assert.All(results.Where(r => r.Kind == "full"), r => Assert.Equal(expectedFull, r.Ids));
        Assert.All(results.Where(r => r.Kind == "quick"), r => Assert.Equal(expectedQuick, r.Ids));
    }
}
