using CatchIt.Core.Data;
using CatchIt.Core.Search;

namespace CatchIt.Tests;

/// <summary>
/// Tests for how typed text is matched against products (user story #5): word and word-prefix matching,
/// safe handling of whatever the user types, <see cref="SearchQuery"/>, and keeping the full-text index in
/// step with the products table. Searches go through <see cref="ListingSearchService"/>, the one the app uses.
/// </summary>
public class SearchMatchingTests : TempDatabaseTests
{
    private readonly ListingSearchService _search;

    public SearchMatchingTests()
    {
        _search = new ListingSearchService(Db);

        Add("Apex Wireless Headphones", "Apex", 3, "headphones audio wireless");
        Add("Orbit Wireless Headphones", "Orbit", 2, "headphones audio wireless");
        Add("Zenith 4K Monitor 27in", "Zenith", 3, "monitor display screen 4k");
        Add("XJ-9 Studio Cans", "Generic", 1, "headphones studio");
    }

    /// <summary>Adds a product with one priced listing, so it can show up in a search.</summary>
    private long Add(string name, string brand, int tier, string keywords)
    {
        var product = TestCatalog.AddProduct(Db, name, brand, tier, keywords);
        TestCatalog.AddListing(Db, product);
        return product;
    }

    /// <summary>The names of the distinct products a search finds.</summary>
    private List<string> Names(string? text) =>
        _search.Search(text, limit: 500).Hits.Select(h => h.ProductName).Distinct().ToList();

    // ---- Matching ----

    [Fact]
    public void Search_FindsAWholeWord() =>
        Assert.Equal(new[] { "Zenith 4K Monitor 27in" }, Names("monitor"));

    [Fact]
    public void Search_FindsTheStartOfAWord() =>
        Assert.Equal(3, Names("headphon").Count); // two by name and keyword, one by keyword only

    [Fact]
    public void Search_IgnoresCase()
    {
        Assert.Equal(Names("wireless"), Names("WiReLeSs"));
        Assert.Equal(2, Names("WIRELESS").Count);
    }

    [Fact]
    public void Search_RequiresEveryWordToMatch() =>
        Assert.Equal(new[] { "Apex Wireless Headphones" }, Names("wireless apex"));

    [Fact]
    public void Search_WordsCanMatchDifferentFields() =>
        // "studio" is a keyword and "XJ" is in the name of the same product.
        Assert.Equal(new[] { "XJ-9 Studio Cans" }, Names("studio xj"));

    [Fact]
    public void Search_MatchesKeywordsThatAreNotInTheName() =>
        // "XJ-9 Studio Cans" never says "headphones" in its name, only in its keywords.
        Assert.Contains("XJ-9 Studio Cans", Names("headphones"));

    [Fact]
    public void Search_MatchesNumbers()
    {
        Assert.Equal(new[] { "Zenith 4K Monitor 27in" }, Names("27in"));
        Assert.Equal(new[] { "XJ-9 Studio Cans" }, Names("9"));
    }

    [Fact]
    public void Search_DoesNotMatchTheMiddleOfAWord() =>
        Assert.Empty(Names("eadphone"));

    [Fact]
    public void Search_NoMatch_ReturnsAnEmptyPage() =>
        Assert.Empty(Names("refrigerator"));

    // ---- Whatever the user types ----

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
        Assert.Null(Record.Exception(() => _search.Search(text)));
        Assert.Null(Record.Exception(() => _search.QuickSearch(text)));
        Assert.Equal(4, TestCatalog.Count(Db, "SELECT COUNT(*) FROM Products")); // nothing was damaged
    }

    [Fact]
    public void Search_OperatorWordsAreJustWords()
    {
        // "AND" typed by the user is plain text, so it does not combine anything; every word must match.
        Assert.Empty(Names("wireless AND monitor"));
        Assert.Equal(2, Names("wireless audio").Count);
    }

    [Fact]
    public void Search_AccentsAndUnusualLettersAreAccepted()
    {
        Add("Café Grinder Pro", "Mühle", 2, "kitchen");

        Assert.Single(Names("café"));
        Assert.Single(Names("cafe")); // the index ignores accents
    }

    // ---- SearchQuery ----

    [Fact]
    public void Build_QuotesEachWordAsAPrefixMatch() =>
        Assert.Equal("\"Wireless\"* \"Head\"*", SearchQuery.Build("  Wireless   Head!  "));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  \t ")]
    [InlineData("?!.,")]
    public void Build_WithNothingSearchable_ReturnsNull(string? text) =>
        Assert.Null(SearchQuery.Build(text));

    [Fact]
    public void Build_SplitsOnPunctuation_SoModelNumbersBecomeTwoWords() =>
        Assert.Equal("\"XJ\"* \"9\"*", SearchQuery.Build("XJ-9"));

    [Fact]
    public void Build_StripsQuotesSoTheyCannotEscapeTheQuoting() =>
        Assert.Equal("\"OR\"* \"x\"*", SearchQuery.Build("\" OR \"x\" "));

    [Fact]
    public void Build_UsesAtMostEightWords() =>
        Assert.Equal(SearchQuery.MaxWords, SearchQuery.Build("a b c d e f g h i j k l")!.Split(' ').Length);

    [Fact]
    public void Build_CutsVeryLongWordsShort() =>
        // the word plus two quotes and a star
        Assert.Equal(SearchQuery.MaxWordLength + 3, SearchQuery.Build(new string('x', 500))!.Length);

    // ---- The index stays in step with the products ----

    [Fact]
    public void Index_FindsProductsAddedLater()
    {
        Add("Brand New Toaster", "Toastco", 2, "kitchen");

        Assert.Single(Names("toaster"));
    }

    [Fact]
    public void Index_ForgetsDeletedProducts()
    {
        Sql("DELETE FROM Listings WHERE ProductId IN (SELECT Id FROM Products WHERE Name = 'Zenith 4K Monitor 27in')");
        Sql("DELETE FROM Products WHERE Name = 'Zenith 4K Monitor 27in'");

        Assert.Empty(Names("monitor"));
        Assert.Equal(0, TestCatalog.Count(Db, "SELECT COUNT(*) FROM ProductSearch WHERE ProductSearch MATCH '\"monitor\"*'"));
    }

    [Fact]
    public void Index_FollowsRenamedProducts()
    {
        Sql("UPDATE Products SET Name = 'Zenith Ultrawide Display' WHERE Name = 'Zenith 4K Monitor 27in'");

        Assert.Single(Names("ultrawide"));
        Assert.Empty(Names("27in")); // the old name no longer matches
    }

    [Fact]
    public void Initialize_AddsAndFillsTheIndexOnADatabaseThatNeverHadOne()
    {
        // Simulate a database made before search existed: products present, no search index.
        Sql("""
            DROP TRIGGER Products_search_insert;
            DROP TRIGGER Products_search_delete;
            DROP TRIGGER Products_search_update;
            DROP TABLE ProductSearch;
            """);

        Db.Initialize();

        Assert.Equal(2, Names("wireless").Count);
        Add("After Upgrade Lamp", "Lumo", 2, "lighting");
        Assert.Single(Names("lamp")); // and the triggers are back
    }

    [Fact]
    public void Initialize_CalledAgain_DoesNotDisturbTheIndex()
    {
        Db.Initialize();
        Db.Initialize();

        Assert.Equal(3, Names("headphones").Count);
    }

    [Fact]
    public async Task SearchesKeepWorkingWhileProductsAreBeingAdded()
    {
        var writer = Task.Run(() =>
        {
            for (var i = 0; i < 40; i++)
                Add($"Concurrent Lamp {i}", "Lumo", 2, "lighting");
        });
        var readers = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < 40; i++)
            {
                var page = _search.QuickSearch("lamp");
                Assert.True(page.Hits.Count <= page.TotalProducts); // never a half-written state
            }
        })).ToArray();

        await Task.WhenAll(readers.Append(writer));
        Assert.Equal(40, Names("lamp").Count);
    }
}
