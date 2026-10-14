using CatchIt.Core.Data;
using CatchIt.Core.Search;
using Microsoft.Data.Sqlite;

namespace CatchIt.Tests;

/// <summary>
/// Tests for <see cref="ProductSearchService"/> and the full-text index behind it (user story #5):
/// word and word-prefix matching, safe handling of whatever the user types, and keeping the
/// index in step with the products table. Each test gets its own throwaway database file.
/// </summary>
public class ProductSearchServiceTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"catchit-test-{Guid.NewGuid():N}.db");
    private readonly Database _db;
    private readonly ProductSearchService _search;

    public ProductSearchServiceTests()
    {
        _db = new Database(_path);
        _db.Initialize();
        _search = new ProductSearchService(_db);

        TestCatalog.AddProduct(_db, "Apex Wireless Headphones", "Apex", 3, "headphones audio wireless");
        TestCatalog.AddProduct(_db, "Orbit Wireless Headphones", "Orbit", 2, "headphones audio wireless");
        TestCatalog.AddProduct(_db, "Zenith 4K Monitor 27in", "Zenith", 3, "monitor display screen 4k");
        TestCatalog.AddProduct(_db, "XJ-9 Studio Cans", "Generic", 1, "headphones studio");
    }

    public void Dispose()
    {
        // SQLite keeps pooled connections open, which would stop the file being deleted.
        SqliteConnection.ClearAllPools();
        if (File.Exists(_path)) File.Delete(_path);
    }

    private static IEnumerable<string> Names(SearchPage page) => page.Hits.Select(h => h.Name);

    // ---- Matching ----

    [Fact]
    public void Search_FindsAWholeWord()
    {
        var page = _search.Search("monitor");

        Assert.Equal(new[] { "Zenith 4K Monitor 27in" }, Names(page));
        Assert.Equal(1, page.TotalMatches);
    }

    [Fact]
    public void Search_FindsTheStartOfAWord()
    {
        var page = _search.Search("headphon");

        Assert.Equal(3, page.TotalMatches); // two by name and keyword, one by keyword only
    }

    [Fact]
    public void Search_IgnoresCase()
    {
        Assert.Equal(_search.Search("wireless").TotalMatches, _search.Search("WiReLeSs").TotalMatches);
        Assert.Equal(2, _search.Search("WIRELESS").TotalMatches);
    }

    [Fact]
    public void Search_RequiresEveryWordToMatch()
    {
        var page = _search.Search("wireless apex");

        Assert.Equal(new[] { "Apex Wireless Headphones" }, Names(page));
    }

    [Fact]
    public void Search_WordsCanMatchDifferentFields()
    {
        // "studio" is a keyword and "XJ" is in the name of the same product.
        Assert.Equal(new[] { "XJ-9 Studio Cans" }, Names(_search.Search("studio xj")));
    }

    [Fact]
    public void Search_MatchesKeywordsThatAreNotInTheName()
    {
        // "XJ-9 Studio Cans" never says "headphones" in its name, only in its keywords.
        Assert.Contains("XJ-9 Studio Cans", Names(_search.Search("headphones")));
    }

    [Fact]
    public void Search_MatchesNumbers()
    {
        Assert.Equal(new[] { "Zenith 4K Monitor 27in" }, Names(_search.Search("27in")));
        Assert.Equal(new[] { "XJ-9 Studio Cans" }, Names(_search.Search("9")));
    }

    [Fact]
    public void Search_DoesNotMatchTheMiddleOfAWord()
    {
        // The deliberate trade-off for a small, fast index: matches start at the beginning of a word.
        Assert.Equal(0, _search.Search("eadphone").TotalMatches);
    }

    [Fact]
    public void Search_NoMatch_ReturnsAnEmptyPage()
    {
        var page = _search.Search("refrigerator");

        Assert.Empty(page.Hits);
        Assert.Equal(0, page.TotalMatches);
    }

    [Fact]
    public void Search_ReturnsTheBrandNameAndQualityTier()
    {
        var hit = _search.Search("monitor").Hits.Single();

        Assert.Equal("Zenith", hit.BrandName);
        Assert.Equal(3, hit.QualityTier);
        Assert.True(hit.ProductId > 0);
    }

    // ---- Limits ----

    [Fact]
    public void Search_LimitsTheHitsButReportsTheTotal()
    {
        for (var i = 0; i < 60; i++)
            TestCatalog.AddProduct(_db, $"Bulk Gadget {i}", "Bulk", 1, "gadget");

        var page = _search.Search("gadget", limit: 10);

        Assert.Equal(10, page.Hits.Count);
        Assert.Equal(60, page.TotalMatches);
    }

    [Fact]
    public void Search_LimitIsKeptWithinSensibleBounds()
    {
        for (var i = 0; i < 5; i++)
            TestCatalog.AddProduct(_db, $"Bounded Gadget {i}", "Bulk", 1, "gadget");

        Assert.Single(_search.Search("gadget", limit: 0).Hits);
        Assert.Single(_search.Search("gadget", limit: -5).Hits);
        Assert.Equal(5, _search.Search("gadget", limit: 100000).Hits.Count);
    }

    // ---- Whatever the user types ----

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!!!")]
    [InlineData("\"")]
    [InlineData("*")]
    [InlineData("()")]
    public void Search_EmptyOrSymbolOnlyText_ReturnsNothingWithoutFailing(string? text)
    {
        var page = _search.Search(text);

        Assert.Empty(page.Hits);
        Assert.Equal(0, page.TotalMatches);
    }

    [Theory]
    [InlineData("wireless AND")]
    [InlineData("AND OR NOT")]
    [InlineData("NEAR(wireless headphones)")]
    [InlineData("name:wireless")]
    [InlineData("keywords : monitor")]
    [InlineData("-wireless")]
    [InlineData("^wireless")]
    [InlineData("wire*less")]
    [InlineData("\"unclosed quote wireless")]
    [InlineData("wireless' OR 1=1 --")]
    [InlineData("wireless); DROP TABLE Products; --")]
    [InlineData("wireless\0headphones")]
    public void Search_SearchSyntaxTypedByTheUser_NeverThrows(string text)
    {
        var ex = Record.Exception(() => _search.Search(text));

        Assert.Null(ex);
        Assert.Equal(4, TestCatalog.Count(_db, "SELECT COUNT(*) FROM Products")); // nothing was damaged
    }

    [Fact]
    public void Search_OperatorWordsAreJustWords()
    {
        // "AND" typed by the user is plain text, so it does not combine anything; every word must match.
        Assert.Equal(0, _search.Search("wireless AND monitor").TotalMatches);
        Assert.Equal(2, _search.Search("wireless audio").TotalMatches);
    }

    [Fact]
    public void Search_AccentsAndUnusualLettersAreAccepted()
    {
        TestCatalog.AddProduct(_db, "Café Grinder Pro", "Mühle", 2, "kitchen");

        Assert.Single(_search.Search("café").Hits);
        Assert.Single(_search.Search("cafe").Hits); // the index ignores accents
    }

    // ---- BuildQuery ----

    [Fact]
    public void BuildQuery_QuotesEachWordAsAPrefixMatch()
    {
        Assert.Equal("\"Wireless\"* \"Head\"*", ProductSearchService.BuildQuery("  Wireless   Head!  "));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  \t ")]
    [InlineData("?!.,")]
    public void BuildQuery_WithNothingSearchable_ReturnsNull(string? text)
    {
        Assert.Null(ProductSearchService.BuildQuery(text));
    }

    [Fact]
    public void BuildQuery_SplitsOnPunctuation_SoModelNumbersBecomeTwoWords()
    {
        Assert.Equal("\"XJ\"* \"9\"*", ProductSearchService.BuildQuery("XJ-9"));
    }

    [Fact]
    public void BuildQuery_StripsQuotesSoTheyCannotEscapeTheQuoting()
    {
        var query = ProductSearchService.BuildQuery("\" OR \"x\" ")!;

        Assert.Equal("\"OR\"* \"x\"*", query);
    }

    [Fact]
    public void BuildQuery_UsesAtMostEightWords()
    {
        var query = ProductSearchService.BuildQuery("a b c d e f g h i j k l")!;

        Assert.Equal(ProductSearchService.MaxWords, query.Split(' ').Length);
    }

    [Fact]
    public void BuildQuery_CutsVeryLongWordsShort()
    {
        var query = ProductSearchService.BuildQuery(new string('x', 500))!;

        Assert.Equal(ProductSearchService.MaxWordLength + 3, query.Length); // the word plus two quotes and a star
    }

    // ---- The index stays in step with the products ----

    [Fact]
    public void Index_FindsProductsAddedLater()
    {
        TestCatalog.AddProduct(_db, "Brand New Toaster", "Toastco", 2, "kitchen");

        Assert.Single(_search.Search("toaster").Hits);
    }

    [Fact]
    public void Index_ForgetsDeletedProducts()
    {
        using (var conn = _db.Open())
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "DELETE FROM Products WHERE Name = 'Zenith 4K Monitor 27in'";
            cmd.ExecuteNonQuery();
        }

        Assert.Equal(0, _search.Search("monitor").TotalMatches);
    }

    [Fact]
    public void Index_FollowsRenamedProducts()
    {
        using (var conn = _db.Open())
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "UPDATE Products SET Name = 'Zenith Ultrawide Display' WHERE Name = 'Zenith 4K Monitor 27in'";
            cmd.ExecuteNonQuery();
        }

        Assert.Single(_search.Search("ultrawide").Hits);
        Assert.Equal(0, _search.Search("27in").TotalMatches); // the old name no longer matches
    }

    [Fact]
    public void Initialize_AddsAndFillsTheIndexOnADatabaseThatNeverHadOne()
    {
        // Simulate a database made before search existed: products present, no search index.
        using (var conn = _db.Open())
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                DROP TRIGGER Products_search_insert;
                DROP TRIGGER Products_search_delete;
                DROP TRIGGER Products_search_update;
                DROP TABLE ProductSearch;
                """;
            cmd.ExecuteNonQuery();
        }

        _db.Initialize();

        Assert.Equal(2, _search.Search("wireless").TotalMatches);
        TestCatalog.AddProduct(_db, "After Upgrade Lamp", "Lumo", 2, "lighting");
        Assert.Single(_search.Search("lamp").Hits); // and the triggers are back
    }

    [Fact]
    public void Initialize_CalledAgain_DoesNotDisturbTheIndex()
    {
        _db.Initialize();
        _db.Initialize();

        Assert.Equal(3, _search.Search("headphones").TotalMatches);
    }
}
